import os
import numpy as np
import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.ensemble import GradientBoostingClassifier
from sklearn.metrics import accuracy_score
import joblib


SCRIPTS_DIR = os.path.dirname(os.path.abspath(__file__))
BASE_DIR = os.path.dirname(SCRIPTS_DIR)
MODELS_DIR = os.path.join(BASE_DIR, 'ml_api', 'models')
DATA_DIR = os.path.join(BASE_DIR, 'data')
os.makedirs(MODELS_DIR, exist_ok=True)
os.makedirs(DATA_DIR, exist_ok=True)

# Imputation medians — must match recalculate_service constants
DAYS_UNTIL_CLOSE_MEDIAN = 30
HIST_CLOSE_RATE_MEDIAN = 50.0

DEFAULT_TRAIN_ROWS = int(os.environ.get("ML_TRAIN_ROWS", "1000000"))
RANDOM_SEED = int(os.environ.get("ML_RANDOM_SEED", "42"))
HIGH_VALUE_RATIO = float(os.environ.get("ML_HIGH_VALUE_RATIO", "0.03"))
MAX_DEAL_VALUE = float(os.environ.get("ML_MAX_DEAL_VALUE", "5000000"))
MIN_ACCURACY = float(os.environ.get("ML_MIN_ACCURACY", "0.70"))
ENFORCE_MIN_ACCURACY = os.environ.get("ML_ENFORCE_ACCURACY", "0").lower() in ("1", "true", "yes")
CLOSURE_LABEL_NOISE = float(os.environ.get("ML_CLOSURE_LABEL_NOISE", "0.03"))
CHURN_LABEL_NOISE = float(os.environ.get("ML_CHURN_LABEL_NOISE", "0.04"))

CLOSURE_FEATURES = (
    "avg_deal_value_log",
    "deal_value_log",
    "days_since_created",
    "stage_encoded",
    "num_interactions",
    "days_until_expected_close",
    "historical_close_rate",
    "client_lifetime_value_log",
    "client_tenure_days",
)

CHURN_FEATURES = (
    "days_since_last_contact",
    "num_open_deals",
    "avg_deal_value_log",
    "historical_close_rate",
    "client_lifetime_value_log",
    "client_tenure_days",
    "days_until_expected_close",
)


def _sample_deal_values(rng, size, high_value_ratio=HIGH_VALUE_RATIO, max_deal_value=MAX_DEAL_VALUE):
    base = rng.lognormal(mean=10.8, sigma=0.8, size=size)
    high_mask = rng.random(size) < 0.026
    if high_mask.any():
        base[high_mask] = rng.lognormal(mean=13.6, sigma=0.5, size=high_mask.sum())
    base = np.clip(base, 500.0, max_deal_value)
    return np.round(base, 2)


def _apply_missing(values, missing_rate, impute_value, rng):
    mask = rng.random(values.shape[0]) < missing_rate
    result = values.copy()
    result[mask] = impute_value
    return result


def _build_expected_close(days_until, base_date):
    return base_date + pd.to_timedelta(days_until, unit="D")


def _sigmoid(values):
    return 1.0 / (1.0 + np.exp(-values))


def _apply_label_noise(labels, noise_rate, rng):
    if noise_rate <= 0:
        return labels
    noisy = labels.copy()
    flip_mask = rng.random(labels.shape[0]) < noise_rate
    noisy[flip_mask] = 1 - noisy[flip_mask]
    return noisy


def _check_accuracy(model_name, acc):
    print(f"{model_name} model accuracy: {acc * 100:.2f}%")
    if acc < MIN_ACCURACY:
        message = f"{model_name} accuracy below {MIN_ACCURACY * 100:.2f}%"
        if ENFORCE_MIN_ACCURACY:
            raise Exception(message)
        print(f"warning: {message}")


def generate_and_train_closure(row_count, rng):
    print(f"[1/2] generation and training of closure model ({row_count} rows)...")

    deal_value = _sample_deal_values(rng, row_count)
    avg_deal_value = np.clip(deal_value * rng.normal(1.0, 0.15, size=row_count), 100.0, None)
    days_since_created = rng.integers(1, 365, size=row_count)
    stage_encoded = rng.integers(1, 5, size=row_count)
    num_interactions = np.clip(days_since_created // 7 + rng.integers(0, 5, size=row_count), 0, 120)

    days_until_raw = rng.integers(-60, 365, size=row_count)
    days_until_expected_close = _apply_missing(days_until_raw, 0.2, DAYS_UNTIL_CLOSE_MEDIAN, rng)

    hist_close_rate_raw = rng.uniform(0, 100, size=row_count)
    historical_close_rate = _apply_missing(hist_close_rate_raw, 0.2, HIST_CLOSE_RATE_MEDIAN, rng)

    client_tenure_days = rng.integers(30, 3650, size=row_count)
    client_lifetime_value = avg_deal_value * rng.uniform(1.0, 12.0, size=row_count)

    base_date = pd.Timestamp.now('UTC').normalize()
    expected_close = _build_expected_close(days_until_expected_close, base_date)

    avg_deal_value_log = np.log1p(avg_deal_value)
    deal_value_log = np.log1p(deal_value)
    client_lifetime_value_log = np.log1p(client_lifetime_value)

    logit = (
        -2.5
        + stage_encoded * 0.3
        + num_interactions * 0.01
        - days_since_created * 0.002
        - days_until_expected_close * 0.001
        + historical_close_rate * 0.005
        + avg_deal_value_log * 0.08
        + deal_value_log * 0.05
        + client_lifetime_value_log * 0.02
        - client_tenure_days * 0.0001
    )
    base_prob = _sigmoid(logit)
    is_closed = (base_prob > 0.5).astype(int)
    is_closed = _apply_label_noise(is_closed, CLOSURE_LABEL_NOISE, rng)

    df = pd.DataFrame(
        {
            "deal_value": deal_value,
            "avg_deal_value": avg_deal_value,
            "days_since_created": days_since_created,
            "stage_encoded": stage_encoded,
            "num_interactions": num_interactions,
            "days_until_expected_close": days_until_expected_close,
            "historical_close_rate": np.round(historical_close_rate, 1),
            "client_lifetime_value": np.round(client_lifetime_value, 2),
            "client_tenure_days": client_tenure_days,
            "expected_close": expected_close,
            "closure_score": np.round(base_prob * 100, 1),
            "is_closed": is_closed.astype(int),
        }
    )

    df["avg_deal_value_log"] = np.log1p(df["avg_deal_value"])
    df["deal_value_log"] = np.log1p(df["deal_value"])
    df["client_lifetime_value_log"] = np.log1p(df["client_lifetime_value"])

    df.to_csv(os.path.join(DATA_DIR, "synthetic_closure.csv"), index=False)

    X = df[list(CLOSURE_FEATURES)]
    y = df["is_closed"]

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    model = GradientBoostingClassifier(random_state=42)
    model.fit(X_train, y_train)

    acc = accuracy_score(y_test, model.predict(X_test))
    _check_accuracy("closure", acc)

    save_path = os.path.join(MODELS_DIR, "closure_model.pkl")
    joblib.dump(model, save_path)
    print(f"saved: {save_path}\n")
    return model


def generate_and_train_churn(row_count, rng):
    print(f"[2/2] generation and training of churn model ({row_count} rows)...")

    days_since_last_contact = rng.integers(1, 365, size=row_count)
    num_open_deals = rng.integers(0, 6, size=row_count)
    avg_deal_value = _sample_deal_values(rng, row_count)

    hist_close_rate_raw = rng.uniform(0, 100, size=row_count)
    historical_close_rate = _apply_missing(hist_close_rate_raw, 0.2, HIST_CLOSE_RATE_MEDIAN, rng)

    days_until_raw = rng.integers(-60, 365, size=row_count)
    days_until_expected_close = _apply_missing(days_until_raw, 0.2, DAYS_UNTIL_CLOSE_MEDIAN, rng)

    client_tenure_days = rng.integers(30, 3650, size=row_count)
    client_lifetime_value = avg_deal_value * rng.uniform(1.0, 12.0, size=row_count)

    base_date = pd.Timestamp.now('UTC').normalize()
    expected_close = _build_expected_close(days_until_expected_close, base_date)

    avg_deal_value_log = np.log1p(avg_deal_value)
    client_lifetime_value_log = np.log1p(client_lifetime_value)

    logit = (
        0.6
        + days_since_last_contact * 0.005
        - num_open_deals * 0.2
        - historical_close_rate * 0.01
        - client_tenure_days * 0.0002
        - client_lifetime_value_log * 0.15
        + avg_deal_value_log * 0.1
        + days_until_expected_close * 0.001
    )
    base_prob = _sigmoid(logit)
    is_churned = (base_prob > 0.5).astype(int)
    is_churned = _apply_label_noise(is_churned, CHURN_LABEL_NOISE, rng)

    df = pd.DataFrame(
        {
            "days_since_last_contact": days_since_last_contact,
            "num_open_deals": num_open_deals,
            "avg_deal_value": avg_deal_value,
            "historical_close_rate": np.round(historical_close_rate, 1),
            "client_lifetime_value": np.round(client_lifetime_value, 2),
            "client_tenure_days": client_tenure_days,
            "days_until_expected_close": days_until_expected_close,
            "expected_close": expected_close,
            "churn_score": np.round(base_prob * 100, 1),
            "is_churned": is_churned.astype(int),
        }
    )

    df["avg_deal_value_log"] = np.log1p(df["avg_deal_value"])
    df["client_lifetime_value_log"] = np.log1p(df["client_lifetime_value"])

    df.to_csv(os.path.join(DATA_DIR, "synthetic_churn.csv"), index=False)

    X = df[list(CHURN_FEATURES)]
    y = df["is_churned"]

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    model = GradientBoostingClassifier(random_state=42)
    model.fit(X_train, y_train)

    acc = accuracy_score(y_test, model.predict(X_test))
    _check_accuracy("churn", acc)

    save_path = os.path.join(MODELS_DIR, "churn_model.pkl")
    joblib.dump(model, save_path)
    print(f"saved: {save_path}\n")
    return model


if __name__ == "__main__":
    print("starting...")
    rng = np.random.default_rng(RANDOM_SEED)
    generate_and_train_closure(DEFAULT_TRAIN_ROWS, rng)
    generate_and_train_churn(DEFAULT_TRAIN_ROWS, rng)
    print("models trained and saved successfully!")