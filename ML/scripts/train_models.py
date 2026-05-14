import os
import random
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


def generate_and_train_closure():
    print("[1/2] generation and training of closure model...")
    data = []
    for _ in range(100000):
        deal_value = round(np.random.lognormal(mean=10.8, sigma=1.1), 2)
        days_since_created = np.random.randint(1, 120)
        stage_encoded = np.random.randint(0, 5)
        num_interactions = np.random.randint(0, 25)
        # 5th feature: days until expected close; ~20% missing → imputed with median
        days_until_raw = int(np.random.randint(-30, 181)) if random.random() > 0.2 else None
        days_until_close = days_until_raw if days_until_raw is not None else DAYS_UNTIL_CLOSE_MEDIAN

        base_prob = (0.05 + stage_encoded * 0.10 + num_interactions * 0.01
                     - days_since_created * 0.003 - days_until_close * 0.001)
        base_prob = np.clip(base_prob, 0.05, 0.95)

        data.append({
            'deal_value': deal_value,
            'days_since_created': days_since_created,
            'stage_encoded': stage_encoded,
            'num_interactions': num_interactions,
            'days_until_expected_close': days_until_close,
            'closure_score': round(base_prob * 100, 1),
            'is_closed': 1 if random.random() < base_prob else 0
        })

    df = pd.DataFrame(data)
    df.to_csv(os.path.join(DATA_DIR, 'synthetic_closure.csv'), index=False)

    X = df[['deal_value', 'days_since_created', 'stage_encoded', 'num_interactions', 'days_until_expected_close']]
    y = df['is_closed']

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    model = GradientBoostingClassifier(random_state=42)
    model.fit(X_train, y_train)

    acc = accuracy_score(y_test, model.predict(X_test))
    print(f"closure model accuracy: {acc * 100:.2f}%")
    if acc < 0.70:
        raise Exception("accuracy below 70%")

    save_path = os.path.join(MODELS_DIR, 'closure_model.pkl')
    joblib.dump(model, save_path)
    print(f"saved: {save_path}\n")


def generate_and_train_churn():
    print("[2/2] generation and training of churn model...")
    data = []
    for _ in range(100000):
        days_since_last_contact = np.random.randint(1, 365)
        num_open_deals = np.random.randint(0, 5)
        avg_deal_value = round(np.random.lognormal(mean=9.4, sigma=1.0), 2)
        # 4th feature: historical close rate (%); ~20% missing → imputed with median
        hist_rate_raw = round(random.uniform(0, 100), 1) if random.random() > 0.2 else None
        hist_close_rate = hist_rate_raw if hist_rate_raw is not None else HIST_CLOSE_RATE_MEDIAN

        base_prob = (0.12 + days_since_last_contact * 0.002 - num_open_deals * 0.12
                     - hist_close_rate * 0.003)
        base_prob = np.clip(base_prob, 0.05, 0.95)
        enterprise_stickiness = (avg_deal_value / 10000) * 0.01
        base_prob += enterprise_stickiness

        data.append({
            'days_since_last_contact': days_since_last_contact,
            'num_open_deals': num_open_deals,
            'avg_deal_value': avg_deal_value,
            'historical_close_rate': hist_close_rate,
            'churn_score': round(base_prob * 100, 1),
            'is_churned': 1 if random.random() < base_prob else 0
        })

    df = pd.DataFrame(data)
    df.to_csv(os.path.join(DATA_DIR, 'synthetic_churn.csv'), index=False)

    X = df[['days_since_last_contact', 'num_open_deals', 'avg_deal_value', 'historical_close_rate']]
    y = df['is_churned']

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42)
    model = GradientBoostingClassifier(random_state=42)
    model.fit(X_train, y_train)

    acc = accuracy_score(y_test, model.predict(X_test))
    print(f"churn model accuracy: {acc * 100:.2f}%")
    if acc < 0.70:
        raise Exception("accuracy below 70%")

    save_path = os.path.join(MODELS_DIR, 'churn_model.pkl')
    joblib.dump(model, save_path)
    print(f"saved: {save_path}\n")

if __name__ == '__main__':
    print("starting...")
    generate_and_train_closure()
    generate_and_train_churn()
    print("models trained and saved successfully!")