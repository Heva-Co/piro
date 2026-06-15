using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Piro.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuartzSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS qrtz_job_details (
    sched_name        VARCHAR(120) NOT NULL,
    job_name          VARCHAR(200) NOT NULL,
    job_group         VARCHAR(200) NOT NULL,
    description       VARCHAR(250),
    job_class_name    VARCHAR(250) NOT NULL,
    is_durable        BOOL         NOT NULL,
    is_nonconcurrent  BOOL         NOT NULL,
    is_update_data    BOOL         NOT NULL,
    requests_recovery BOOL         NOT NULL,
    job_data          BYTEA,
    CONSTRAINT pk_qrtz_job_details PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_triggers (
    sched_name     VARCHAR(120) NOT NULL,
    trigger_name   VARCHAR(200) NOT NULL,
    trigger_group  VARCHAR(200) NOT NULL,
    job_name       VARCHAR(200) NOT NULL,
    job_group      VARCHAR(200) NOT NULL,
    description    VARCHAR(250),
    next_fire_time BIGINT,
    prev_fire_time BIGINT,
    priority              INT,
    trigger_state         VARCHAR(16)  NOT NULL,
    misfire_orig_fire_time BIGINT,
    trigger_type   VARCHAR(8)   NOT NULL,
    start_time     BIGINT       NOT NULL,
    end_time       BIGINT,
    calendar_name  VARCHAR(200),
    misfire_instr  SMALLINT,
    job_data       BYTEA,
    CONSTRAINT pk_qrtz_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_triggers_job_details FOREIGN KEY (sched_name, job_name, job_group)
        REFERENCES qrtz_job_details (sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_simple_triggers (
    sched_name        VARCHAR(120) NOT NULL,
    trigger_name      VARCHAR(200) NOT NULL,
    trigger_group     VARCHAR(200) NOT NULL,
    repeat_count      BIGINT       NOT NULL,
    repeat_interval   BIGINT       NOT NULL,
    times_triggered   BIGINT       NOT NULL,
    CONSTRAINT pk_qrtz_simple_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_simple_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_simprop_triggers (
    sched_name    VARCHAR(120)   NOT NULL,
    trigger_name  VARCHAR(200)   NOT NULL,
    trigger_group VARCHAR(200)   NOT NULL,
    str_prop_1    VARCHAR(512),
    str_prop_2    VARCHAR(512),
    str_prop_3    VARCHAR(512),
    int_prop_1    INT,
    int_prop_2    INT,
    long_prop_1   BIGINT,
    long_prop_2   BIGINT,
    dec_prop_1    NUMERIC(13,4),
    dec_prop_2    NUMERIC(13,4),
    bool_prop_1   BOOL,
    bool_prop_2   BOOL,
    time_zone_id  VARCHAR(80),
    CONSTRAINT pk_qrtz_simprop_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_simprop_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_cron_triggers (
    sched_name      VARCHAR(120) NOT NULL,
    trigger_name    VARCHAR(200) NOT NULL,
    trigger_group   VARCHAR(200) NOT NULL,
    cron_expression VARCHAR(120) NOT NULL,
    time_zone_id    VARCHAR(80),
    CONSTRAINT pk_qrtz_cron_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_cron_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_blob_triggers (
    sched_name    VARCHAR(120) NOT NULL,
    trigger_name  VARCHAR(200) NOT NULL,
    trigger_group VARCHAR(200) NOT NULL,
    blob_data     BYTEA,
    CONSTRAINT pk_qrtz_blob_triggers PRIMARY KEY (sched_name, trigger_name, trigger_group),
    CONSTRAINT fk_qrtz_blob_triggers FOREIGN KEY (sched_name, trigger_name, trigger_group)
        REFERENCES qrtz_triggers (sched_name, trigger_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_calendars (
    sched_name    VARCHAR(120) NOT NULL,
    calendar_name VARCHAR(200) NOT NULL,
    calendar      BYTEA        NOT NULL,
    CONSTRAINT pk_qrtz_calendars PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE IF NOT EXISTS qrtz_paused_trigger_grps (
    sched_name    VARCHAR(120) NOT NULL,
    trigger_group VARCHAR(200) NOT NULL,
    CONSTRAINT pk_qrtz_paused_trigger_grps PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_fired_triggers (
    sched_name        VARCHAR(120) NOT NULL,
    entry_id          VARCHAR(140) NOT NULL,
    trigger_name      VARCHAR(200) NOT NULL,
    trigger_group     VARCHAR(200) NOT NULL,
    instance_name     VARCHAR(200) NOT NULL,
    fired_time        BIGINT       NOT NULL,
    sched_time        BIGINT       NOT NULL,
    priority          INT          NOT NULL,
    state             VARCHAR(16)  NOT NULL,
    job_name          VARCHAR(200),
    job_group         VARCHAR(200),
    is_nonconcurrent  BOOL,
    requests_recovery BOOL,
    CONSTRAINT pk_qrtz_fired_triggers PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE IF NOT EXISTS qrtz_scheduler_state (
    sched_name        VARCHAR(120) NOT NULL,
    instance_name     VARCHAR(200) NOT NULL,
    last_checkin_time BIGINT       NOT NULL,
    checkin_interval  BIGINT       NOT NULL,
    CONSTRAINT pk_qrtz_scheduler_state PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE IF NOT EXISTS qrtz_locks (
    sched_name VARCHAR(120) NOT NULL,
    lock_name  VARCHAR(40)  NOT NULL,
    CONSTRAINT pk_qrtz_locks PRIMARY KEY (sched_name, lock_name)
);

ALTER TABLE qrtz_triggers ADD COLUMN IF NOT EXISTS misfire_orig_fire_time BIGINT;

CREATE INDEX IF NOT EXISTS idx_qrtz_j_req_recovery  ON qrtz_job_details (sched_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_qrtz_j_grp            ON qrtz_job_details (sched_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_j              ON qrtz_triggers (sched_name, job_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_jg             ON qrtz_triggers (sched_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_c              ON qrtz_triggers (sched_name, calendar_name);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_g              ON qrtz_triggers (sched_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_state          ON qrtz_triggers (sched_name, trigger_state);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_state      ON qrtz_triggers (sched_name, trigger_state, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_misfire     ON qrtz_triggers (sched_name, misfire_instr, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st_misfire  ON qrtz_triggers (sched_name, misfire_instr, next_fire_time, trigger_state);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st_misfire_grp ON qrtz_triggers (sched_name, misfire_instr, next_fire_time, trigger_group, trigger_state);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers (sched_name, instance_name);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_inst_job_req_rcvry ON qrtz_fired_triggers (sched_name, instance_name, requests_recovery);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_j_g           ON qrtz_fired_triggers (sched_name, job_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_jg            ON qrtz_fired_triggers (sched_name, job_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_t_g           ON qrtz_fired_triggers (sched_name, trigger_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_tg            ON qrtz_fired_triggers (sched_name, trigger_group);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS qrtz_fired_triggers;
DROP TABLE IF EXISTS qrtz_paused_trigger_grps;
DROP TABLE IF EXISTS qrtz_scheduler_state;
DROP TABLE IF EXISTS qrtz_locks;
DROP TABLE IF EXISTS qrtz_simple_triggers;
DROP TABLE IF EXISTS qrtz_simprop_triggers;
DROP TABLE IF EXISTS qrtz_cron_triggers;
DROP TABLE IF EXISTS qrtz_blob_triggers;
DROP TABLE IF EXISTS qrtz_triggers;
DROP TABLE IF EXISTS qrtz_job_details;
DROP TABLE IF EXISTS qrtz_calendars;
");
        }
    }
}
