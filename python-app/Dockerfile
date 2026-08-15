FROM python:3.11-alpine

ENV PYTHONUNBUFFERED=1
WORKDIR /app

COPY requirements.txt .
RUN apk add --no-cache --update && \
    pip install --no-cache-dir --upgrade pip && \
    pip install --no-cache-dir -r requirements.txt

COPY app.py .

USER nobody

CMD ["python", "app.py"]
