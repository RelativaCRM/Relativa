import os
import pandas as pd

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
REAL_DATA_PATH = os.path.join(BASE_DIR, 'data', 'real_closure.csv')
FAKE_DATA_PATH = os.path.join(BASE_DIR, 'data', 'synthetic_closure.csv')

REAL_CHURN_PATH = os.path.join(BASE_DIR, 'data', 'real_churn.csv')
FAKE_CHURN_PATH = os.path.join(BASE_DIR, 'data', 'synthetic_churn.csv')

def analyze_data_quality():
    print("Data Quality Reconciliation for Closure started...\n")
    
    df_real = pd.read_csv(REAL_DATA_PATH)
    df_fake = pd.read_csv(FAKE_DATA_PATH)
    
    df_real = df_real[df_real['Opportunity Result'].isin(['Won', 'Loss'])].copy()
    df_real['is_closed_numeric'] = df_real['Opportunity Result'].apply(lambda x: 1 if x == 'Won' else 0)
    
    real_win_rate = df_real['is_closed_numeric'].mean() * 100
    fake_win_rate = df_fake['is_closed'].mean() * 100
    
    real_amount_median = df_real['Opportunity Amount USD'].median()
    real_amount_mean = df_real['Opportunity Amount USD'].mean()
    
    fake_amount_median = df_fake['deal_value'].median()
    fake_amount_mean = df_fake['deal_value'].mean()
    
    print("Порівняння:")
    print("-" * 50)
    
    print(f"   Win Rate (Відсоток успіху):")
    print(f"   Реальність: {real_win_rate:.1f}%")
    print(f"   Синтетика:   {fake_win_rate:.1f}%\n")
    
    print(f"   Сума угоди (Медіана - те, що трапляється найчастіше):")
    print(f"   Реальність: ${real_amount_median:,.0f}")
    print(f"   Синтетика:   ${fake_amount_median:,.0f}\n")
    
    print(f"   Сума угоди (Середнє арифметичне):")
    print(f"   Реальність: ${real_amount_mean:,.0f}")
    print(f"   Синтетика:   ${fake_amount_mean:,.0f}\n")
    
    print("Проблеми Closure (якщо є):")
    if abs(real_win_rate - fake_win_rate) > 10:
        print("- Генератор Win Rate занадто відрізняється від реального!")
    if real_amount_median < fake_amount_median / 2:
        print("- Занадто багато гігантських угод. У реальності переважають дрібні.")

def analyze_churn_quality():
    print("Data Quality Reconciliation for Churn started...\n")
    
    df_real = pd.read_csv(REAL_CHURN_PATH)
    df_fake = pd.read_csv(FAKE_CHURN_PATH)
    
    df_real['is_churned_numeric'] = df_real['Churn'].apply(lambda x: 1 if x == 'Yes' else 0)
    
    real_churn_rate = df_real['is_churned_numeric'].mean() * 100
    fake_churn_rate = df_fake['is_churned'].mean() * 100
    
    real_charge_median = df_real['MonthlyCharges'].median()
    real_charge_mean = df_real['MonthlyCharges'].mean()
    
    fake_value_median = df_fake['avg_deal_value'].median()
    fake_value_mean = df_fake['avg_deal_value'].mean()
    
    print("Порівняння:")
    print("-" * 50)
    
    print(f"   Churn Rate (Відсоток клієнтів, що пішли):")
    print(f"   Реальність: {real_churn_rate:.1f}%")
    print(f"   Синтетика:   {fake_churn_rate:.1f}%\n")
    
    print(f"   Гроші (Медіана):")
    print(f"   Реальність (Щомісячний чек): ${real_charge_median:,.0f}")
    print(f"   Синтетика (B2B Угода):        ${fake_value_median:,.0f}\n")
    
    print(f"   Гроші (Середнє арифметичне):")
    print(f"   Реальність (Щомісячний чек): ${real_charge_mean:,.0f}")
    print(f"   Синтетика (B2B Угода):        ${fake_value_mean:,.0f}\n")

    print("Проблеми Churn (якщо є):")
    if abs(real_churn_rate - fake_churn_rate) > 5:
        print("- Генератор Churn Rate сильно відхиляється від індустріального стандарту")
    if fake_value_mean < fake_value_median * 1.2:
        print("- Аномалія грошей: У нашому B2B датасеті немає 'китів'. Розподіл занадто плоский!")

if __name__ == '__main__':
    analyze_data_quality()
    analyze_churn_quality()