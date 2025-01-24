FROM gvenzl/oracle-free:23-slim-faststart

ENV ORACLE_PASSWORD=root

USER oracle

COPY --chown=oracle:oinstall migrations /container-entrypoint-initdb.d/

EXPOSE 1521
