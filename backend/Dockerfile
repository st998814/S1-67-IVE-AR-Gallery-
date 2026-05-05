FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends gcc libpq-dev \
    && rm -rf /var/lib/apt/lists/*

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py ./app.py
COPY vuforia_service.py ./vuforia_service.py
COPY scripts ./scripts
COPY db ./db

RUN mkdir -p uploads

EXPOSE 5050

CMD ["sh", "-c", "python scripts/migrate.py up && python app.py"]
