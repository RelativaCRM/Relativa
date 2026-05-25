"""Shared ML constants for training and inference."""

# Feature order for GradientBoostingClassifier predictions.
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

# Imputation medians must match recalculate_service constants.
DAYS_UNTIL_CLOSE_MEDIAN = 30
HIST_CLOSE_RATE_MEDIAN = 50.0
