from django.apps import AppConfig


class MlApiConfig(AppConfig):
    default_auto_field = 'django.db.models.BigAutoField'
    name = 'ml_api'
    
    # Глобальні змінні для зберігання моделей в оперативній пам'яті
    closure_model = None
    churn_model = None

    def ready(self):
        # Цей код виконується 1 раз при старті сервера
        base_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
        
        closure_path = os.path.join(base_dir, 'ml_api', 'models', 'closure_model.pkl')
        churn_path = os.path.join(base_dir, 'ml_api', 'models', 'churn_model.pkl')

        # Завантажуємо моделі, якщо файли існують
        if os.path.exists(closure_path) and os.path.exists(churn_path):
            MlApiConfig.closure_model = joblib.load(closure_path)
            MlApiConfig.churn_model = joblib.load(churn_path)
            print("ML models loaded successfully!")
        else:
            print("ML model files not found.")
