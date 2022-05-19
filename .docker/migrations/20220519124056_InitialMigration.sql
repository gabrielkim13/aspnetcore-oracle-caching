-- InitialMigration

CREATE USER "CACHING" IDENTIFIED BY "root";
GRANT ALL PRIVILEGES TO "CACHING";

CREATE TABLE CACHING.Cache
(
    Id                         NVARCHAR2(900)              NOT NULL,
    Value                      BLOB                        NOT NULL,
    ExpiresAtTime              TIMESTAMP(7) WITH TIME ZONE NOT NULL,
    SlidingExpirationInSeconds NUMBER(19),
    AbsoluteExpiration         TIMESTAMP(7) WITH TIME ZONE,
    CONSTRAINT PK_Cache PRIMARY KEY (Id)
);
