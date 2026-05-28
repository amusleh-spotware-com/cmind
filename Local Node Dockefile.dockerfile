FROM ubuntu:24.04

ARG SSH_PUBLIC_KEY
ARG NODE_USER=ctw

RUN apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        openssh-server \
        sudo \
        ca-certificates \
        curl \
        gnupg \
        docker.io \
        coreutils \
        gawk \
        procps && \
    rm -rf /var/lib/apt/lists/*

RUN mkdir -p /run/sshd && \
    useradd -m -s /bin/bash -G sudo,docker ${NODE_USER} && \
    echo "${NODE_USER} ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/${NODE_USER} && \
    mkdir -p /home/${NODE_USER}/.ssh && \
    echo "${SSH_PUBLIC_KEY}" > /home/${NODE_USER}/.ssh/authorized_keys && \
    chmod 700 /home/${NODE_USER}/.ssh && \
    chmod 600 /home/${NODE_USER}/.ssh/authorized_keys && \
    chown -R ${NODE_USER}:${NODE_USER} /home/${NODE_USER}/.ssh && \
    mkdir -p /var/ctw && \
    chown -R ${NODE_USER}:${NODE_USER} /var/ctw && \
    chmod 755 /var/ctw

RUN sed -i 's/#PermitRootLogin.*/PermitRootLogin no/' /etc/ssh/sshd_config && \
    sed -i 's/#PasswordAuthentication.*/PasswordAuthentication no/' /etc/ssh/sshd_config && \
    sed -i 's/#PubkeyAuthentication.*/PubkeyAuthentication yes/' /etc/ssh/sshd_config

EXPOSE 22

CMD ["/usr/sbin/sshd", "-D", "-e"]
