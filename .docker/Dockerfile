FROM oracle/database:19.3.0-ee

ENV ORACLE_PWD=root
ENV ORACLE_PDB=ORCLPDB1

RUN rm $ORACLE_BASE/runUserScripts.sh
COPY runUserScripts.sh $ORACLE_BASE

COPY migrations /opt/oracle/scripts/setup
