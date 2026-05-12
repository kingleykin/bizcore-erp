FROM rabbitmq:3.13-management

# Tải và cài đặt plugin Delayed Message Exchange
RUN apt-get update && apt-get install -y wget && \
    wget https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/releases/download/v3.13.0/rabbitmq_delayed_message_exchange-3.13.0.ez && \
    mv rabbitmq_delayed_message_exchange-3.13.0.ez /plugins/ && \
    rabbitmq-plugins enable rabbitmq_delayed_message_exchange && \
    apt-get remove -y wget && apt-get autoremove -y && rm -rf /var/lib/apt/lists/*
