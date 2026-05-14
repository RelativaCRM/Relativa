FROM python:3.12-slim AS runtime

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

COPY pyproject.toml ./
COPY ml_api/ ml_api/
COPY relativa_ml/ relativa_ml/
COPY scripts/run_api_and_consumer.sh scripts/run_api_and_consumer.sh
COPY manage.py ./

RUN python -m pip install --no-cache-dir --upgrade pip \
    && python -m pip install --no-cache-dir -e . \
    && chmod +x scripts/run_api_and_consumer.sh

EXPOSE 8084

CMD ["bash", "scripts/run_api_and_consumer.sh"]
