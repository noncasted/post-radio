FROM ubuntu:22.04

EXPOSE 80
EXPOSE 22

EXPOSE 7000
EXPOSE 7001
EXPOSE 7002

# Install SSH server, nginx, Docker, tmux, and essential tools
RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y openssh-server nginx sudo nano curl wget apt-transport-https software-properties-common git tmux net-tools ca-certificates gnupg lsb-release && \
    mkdir -p /var/run/sshd && \
    apt-get clean

# Install Docker
RUN mkdir -p /etc/apt/keyrings && \
    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg && \
    echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null && \
    apt-get update && \
    apt-get install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin && \
    apt-get clean

# Configure SSH
RUN sed -i 's/#PermitRootLogin prohibit-password/PermitRootLogin yes/' /etc/ssh/sshd_config
RUN sed -i 's/#PasswordAuthentication yes/PasswordAuthentication yes/' /etc/ssh/sshd_config
RUN sed 's@session\s*required\s*pam_loginuid.so@session optional pam_loginuid.so@g' -i /etc/pam.d/sshd

RUN echo "export ASPIRE_TOKEN=\"${ASPIRE_TOKEN}\"" >> /etc/bash.bashrc
RUN echo "export DB_CONNECTION_STRING=\"${DB_CONNECTION_STRING}\"" >> /etc/bash.bashrc
RUN echo "export MINIO_ENDPOINT=\"${MINIO_ENDPOINT}\"" >> /etc/bash.bashrc
RUN echo "export MINIO_ACCESSKEY=\"${MINIO_ACCESSKEY}\"" >> /etc/bash.bashrc
RUN echo "export MINIO_SECRETKEY=\"${MINIO_SECRETKEY}\"" >> /etc/bash.bashrc
RUN echo "export AUTH_TOKEN=\"${AUTH_TOKEN}\"" >> /etc/bash.bashrc

ENV CONTAINER_PASSWORD=${CONTAINER_PASSWORD}
# Set root password - use double quotes and sh -c to ensure variable expansion
RUN sh -c 'echo "root:${CONTAINER_PASSWORD}" | chpasswd'

# Configure nginx for HTTP only
RUN rm /etc/nginx/sites-enabled/default
RUN echo 'server {\n\
    listen 7000;\n\
    listen [::]:7000;\n\
    server_name _;\n\
\n\
    location / {\n\
        proxy_pass https://localhost:17216;\n\
        proxy_ssl_verify off;\n\
        proxy_http_version 1.1;\n\
        proxy_set_header Upgrade $http_upgrade;\n\
        proxy_set_header Connection "upgrade";\n\
        proxy_set_header Host $host;\n\
        proxy_set_header X-Real-IP $remote_addr;\n\
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n\
        proxy_set_header X-Forwarded-Proto https;\n\
        proxy_set_header X-Forwarded-Host $host;\n\
        proxy_cache_bypass $http_upgrade;\n\
        proxy_redirect off;\n\
    }\n\
}\n\
\n\
server {\n\
    listen 7001;\n\
    listen [::]:7001;\n\
    server_name _;\n\
\n\
    location / {\n\
        proxy_pass https://localhost:7101;\n\
        proxy_ssl_verify off;\n\
        proxy_http_version 1.1;\n\
        proxy_set_header Upgrade $http_upgrade;\n\
        proxy_set_header Connection "upgrade";\n\
        proxy_set_header Host $host;\n\
        proxy_set_header X-Real-IP $remote_addr;\n\
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n\
        proxy_set_header X-Forwarded-Proto https;\n\
        proxy_set_header X-Forwarded-Host $host;\n\
        proxy_cache_bypass $http_upgrade;\n\
        proxy_redirect off;\n\
    }\n\
}\n\
\n\
server {\n\
    listen 7002;\n\
    listen [::]:7002;\n\
    server_name _;\n\
\n\
    location / {\n\
        proxy_pass https://localhost:7102;\n\
        proxy_ssl_verify off;\n\
        proxy_http_version 1.1;\n\
        proxy_set_header Upgrade $http_upgrade;\n\
        proxy_set_header Connection "upgrade";\n\
        proxy_set_header Host $host;\n\
        proxy_set_header X-Real-IP $remote_addr;\n\
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;\n\
        proxy_set_header X-Forwarded-Proto https;\n\
        proxy_set_header X-Forwarded-Host $host;\n\
        proxy_cache_bypass $http_upgrade;\n\
        proxy_redirect off;\n\
    }\n\
}\n\
' > /etc/nginx/sites-available/default && \
    ln -s /etc/nginx/sites-available/default /etc/nginx/sites-enabled/ 
    
# Install .NET SDK using the official installer script
RUN curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --version latest --channel 10.0

# Set up .NET environment variables after installation (system-wide)
ENV PATH="/root/.dotnet:/root/.aspire:$PATH"
ENV DOTNET_ROOT="/root/.dotnet"
RUN echo 'export PATH="/root/.dotnet:/root/.aspire:$PATH"' >> /etc/bash.bashrc && \
    echo 'export DOTNET_ROOT="/root/.dotnet"' >> /etc/bash.bashrc

# Install Aspire
#RUN curl -sSL https://aspire.dev/install.sh | bash && \
RUN dotnet workload install aspire 
RUN curl -sSL https://aspire.dev/install.sh | bash

# Setup .NET dev certificates
RUN dotnet dev-certs https --clean && \
    dotnet dev-certs https --trust && \
    mkdir -p /https && \
    chmod 755 /https && \
    dotnet dev-certs https -ep /https/aspnetapp.pfx -p root

# Clone the project
RUN mkdir /post-radio && \
    cd /post-radio && \
    git clone https://github.com/noncasted/post-radio.git

# Create directory for Data Protection keys
RUN mkdir -p /var/data-protection-keys && \
    chmod 755 /var/data-protection-keys

WORKDIR /post-radio

# Create a startup script to run Docker, SSH, nginx, and Aspire
RUN echo '#!/bin/bash\n\
set -e\n\
\n\
# Set PATH for .NET and Aspire tools\n\
export PATH="/root/.dotnet:/root/.aspire/bin:$PATH"\n\
export DOTNET_ROOT="/root/.dotnet"\n\
\n\
# Start Docker daemon in background\n\
dockerd > /var/log/dockerd.log 2>&1 &\n\
\n\
# Wait for Docker to be ready\n\
echo "Waiting for Docker to start..."\n\
for i in {1..30}; do\n\
    if docker info > /dev/null 2>&1; then\n\
        echo "Docker is ready!"\n\
        break\n\
    fi\n\
    echo "Waiting for Docker... ($i/30)"\n\
    sleep 1\n\
done\n\
\n\
# Export ASPIRE_TOKEN\n\
export MINIO_ENDPOINT="$MINIO_ENDPOINT"\n\
export MINIO_ACCESSKEY="$MINIO_ACCESSKEY"\n\
export MINIO_SECRETKEY="$MINIO_SECRETKEY"\n\
export AUTH_TOKEN="$AUTH_TOKEN"\n\
export DB_CONNECTION_STRING="$DB_CONNECTION_STRING"\n\
export ASPIRE_TOKEN="$ASPIRE_TOKEN"\n\
echo "ASPIRE_TOKEN exported"\n\
\n\
# Navigate to project directory\n\
cd /post-radio/post-radio/Aspire/AppHost\n\
\n\
# Clean the project\n\
echo "Cleaning project..."\n\
dotnet clean --configuration Release\n\
echo "Project cleaned"\n\
\n\
# Start Aspire in background\n\
echo "Starting Aspire..."\n\
aspire run > /var/log/aspire.log 2>&1 &\n\
echo "Aspire started"\n\
\n\
# Start nginx\n\
service nginx start\n\
\n\
# Start SSH in foreground\n\
/usr/sbin/sshd -D' > /start.sh && \
    chmod +x /start.sh


CMD ["/start.sh"]