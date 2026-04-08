# relativa-ml

ML-мікросервіс на **Django 5.1**. Структуру проєкту створено через `django-admin startproject`; додаток **`ml_api`** — через `startapp`.

## Порт

- **8084** (dev): `python manage.py runserver 0.0.0.0:8084`

## Стек

- **Django 5.1**, **Django REST framework**
- **Celery** + **Redis** (broker/result URL у `settings.py`; розклад beat закоментовано — нічний cron **02:00 UTC** потрібно прив’язати до реального модуля задач)
- **scikit-learn** (для майбутніх `closure_score` / `churn_score`)

Залежності описані в `pyproject.toml` (пакети setuptools: `ml_api`, `relativa_ml`). Встановлення:

```bash
python -m pip install -e ".[dev]"
```

## API-заглушка

- `POST /api/ml/recalculate/` — відповідь `{ "status": "accepted", "detail": "stub" }`.

## Команди

```bash
python manage.py migrate
python manage.py runserver 0.0.0.0:8084
```

## Інтеграція

Зовнішній доступ лише через **relativa-gateway** (`/ml/...`). У production не викликати сервіс напряму з браузера.
