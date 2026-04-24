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

def generate_and_train_closure():
    print("[1/2] generation and training of closure model...")
    data = []
    for _ in range(100000):
        deal_value = round(random.uniform(500, 100000), 2)
        days_since_created = np.random.randint(1, 120)
        stage_encoded = np.random.randint(0, 5) 
        num_interactions = np.random.randint(0, 25)

        base_prob = 0.1 + (stage_encoded * 0.15) + (num_interactions * 0.02) - (days_since_created * 0.005)
        base_prob = np.clip(base_prob, 0.05, 0.95)
        
        data.append({
            'deal_value': deal_value,
            'days_since_created': days_since_created,
            'stage_encoded': stage_encoded,
            'num_interactions': num_interactions,
            'closure_score': round(base_prob * 100, 1),
            'is_closed': 1 if random.random() < base_prob else 0
        })

    df = pd.DataFrame(data)
    
    df.to_csv(os.path.join(DATA_DIR, 'synthetic_closure.csv'), index=False) 

    # Тренування
    X = df[['deal_value', 'days_since_created', 'stage_encoded', 'num_interactions']]
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
        avg_deal_value = round(random.uniform(1000, 50000), 2)

        base_prob = 0.1 + (days_since_last_contact * 0.002) - (num_open_deals * 0.15)
        base_prob = np.clip(base_prob, 0.05, 0.95)
        
        data.append({
            'days_since_last_contact': days_since_last_contact,
            'num_open_deals': num_open_deals,
            'avg_deal_value': avg_deal_value,
            'churn_score': round(base_prob * 100, 1),
            'is_churned': 1 if random.random() < base_prob else 0
        })

    df = pd.DataFrame(data)
    
    df.to_csv(os.path.join(DATA_DIR, 'synthetic_churn.csv'), index=False)

    # Тренування
    X = df[['days_since_last_contact', 'num_open_deals', 'avg_deal_value']]
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