CREATE TABLE TABLE_NAME
(
    Id_0      bigint                      NOT NULL,
    Id_1      bigint                      NOT NULL,
    type      character varying(512)      NOT NULL,
    extension character varying(512),
    version   integer,
    payload   bytea,
    id_hash   integer                     NOT NULL,
    type_hash integer                     NOT NULL,
    modified  timestamp without time zone NOT NULL
);

CREATE INDEX ix_TABLE_NAME
    ON TABLE_NAME USING btree
        (id_hash, type_hash);

CREATE OR REPLACE FUNCTION TABLE_NAME_writetostorage(
    _id_hash integer,
    _Id_0 bigint,
    _Id_1 bigint,
    _type_hash integer,
    _type character varying,
    _extension character varying,
    _version integer,
    _payload bytea)
    RETURNS TABLE
            (
                newgrainstateversion integer
            )
    LANGUAGE 'plpgsql'
AS
$function$
DECLARE
    _newGrainStateVersion integer := _version;
    RowCountVar           integer := 0;

BEGIN
    IF _version IS NOT NULL
    THEN
        UPDATE TABLE_NAME
        SET payload  = _payload,
            modified = (now() at time zone 'utc'),
            Version  = Version + 1

        WHERE id_hash = _id_hash
          AND _id_hash IS NOT NULL
          AND type_hash = _type_hash
          AND _type_hash IS NOT NULL
          AND Id_0 = _Id_0
          AND _Id_0 IS NOT NULL
          AND Id_1 = _Id_1
          AND _Id_1 IS NOT NULL
          AND type = _type
          AND _type IS NOT NULL
          AND ((_extension IS NOT NULL AND extension IS NOT NULL AND
                extension = _extension) OR
               _extension IS NULL AND extension IS NULL)
          AND Version IS NOT NULL
          AND Version = _version
          AND _version IS NOT NULL;

        GET DIAGNOSTICS RowCountVar = ROW_COUNT;
        IF RowCountVar > 0
        THEN
            _newGrainStateVersion := _version + 1;
        END IF;
    END IF;

    -- The grain state has not been read. The following locks rather pessimistically
    -- to ensure only one INSERT succeeds.
    IF _version IS NULL
    THEN
        INSERT INTO TABLE_NAME
        (id_hash,
         Id_0,
         Id_1,
         type_hash,
         type,
         extension,
         payload,
         modified,
         Version)
        SELECT _id_hash,
               _Id_0,
               _Id_1,
               _type_hash,
               _type,
               _extension,
               _payload,
               (now() at time zone 'utc'),
               1
        WHERE NOT EXISTS
                  (
                      -- There should not be any version of this grain state.
                      SELECT 1
                      FROM TABLE_NAME
                      WHERE id_hash = _id_hash
                        AND _id_hash IS NOT NULL
                        AND type_hash = _type_hash
                        AND _type_hash IS NOT NULL
                        AND Id_0 = _Id_0
                        AND _Id_0 IS NOT NULL
                        AND Id_1 = _Id_1
                        AND _Id_1 IS NOT NULL
                        AND type = _type
                        AND _type IS NOT NULL
                        AND ((_extension IS NOT NULL AND extension IS NOT NULL AND
                              extension = _extension) OR
                             _extension IS NULL AND extension IS NULL));

        GET DIAGNOSTICS RowCountVar = ROW_COUNT;
        IF RowCountVar > 0
        THEN
            _newGrainStateVersion := 1;
        END IF;
    END IF;

    RETURN QUERY SELECT _newGrainStateVersion AS NewGrainStateVersion;
END

$function$;
