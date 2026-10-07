-- ============================================================
-- 힐링 마을 게임 스키마 (수정판)
-- MySQL 8.0 / InnoDB / utf8mb4
--
-- 변경점: expeditions 테이블에서 생성 컬럼(GENERATED ALWAYS AS)을 제거.
--        "수령 전 탐사는 유저당 1개"는 서버 코드에서 트랜잭션으로 보장한다.
--
-- 규칙
--  - 모든 시각은 UTC (DATETIME(3), 밀리초까지)
--  - 기획 데이터 ID(building_id, item_id, area_id)는 공유 JSON의 문자열 ID
-- ============================================================

SET NAMES utf8mb4;

-- ------------------------------------------------------------
-- users : 계정
-- ------------------------------------------------------------
CREATE TABLE users (
  id           BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  device_id    VARCHAR(128)    NOT NULL,
  nickname     VARCHAR(20)     NOT NULL,
  gold         INT UNSIGNED    NOT NULL DEFAULT 0,
  created_at   DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  last_seen_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (id),
  UNIQUE KEY uq_users_device (device_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- villages : 마을 (유저당 1개)
-- ------------------------------------------------------------
CREATE TABLE villages (
  owner_id   BIGINT UNSIGNED   NOT NULL,
  width      SMALLINT UNSIGNED NOT NULL DEFAULT 40,
  height     SMALLINT UNSIGNED NOT NULL DEFAULT 8,
  like_count INT UNSIGNED      NOT NULL DEFAULT 0,
  updated_at DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3)
                               ON UPDATE CURRENT_TIMESTAMP(3),

  PRIMARY KEY (owner_id),
  CONSTRAINT fk_villages_owner FOREIGN KEY (owner_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- buildings : 마을에 놓인 건물 하나하나
--   (owner_id, x, y) UNIQUE 로 같은 칸 중복 건설을 DB가 막아준다
-- ------------------------------------------------------------
CREATE TABLE buildings (
  building_uid BIGINT UNSIGNED   NOT NULL AUTO_INCREMENT,
  owner_id     BIGINT UNSIGNED   NOT NULL,
  building_id  VARCHAR(40)       NOT NULL,
  x            SMALLINT UNSIGNED NOT NULL,
  y            SMALLINT UNSIGNED NOT NULL,
  created_at   DATETIME(3)       NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (building_uid),
  UNIQUE KEY uq_buildings_tile (owner_id, x, y),
  KEY idx_buildings_owner (owner_id),
  CONSTRAINT fk_buildings_owner FOREIGN KEY (owner_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- inventory : 아이템 보유량 (유저 + 아이템 조합이 곧 PK)
-- ------------------------------------------------------------
CREATE TABLE inventory (
  user_id BIGINT UNSIGNED NOT NULL,
  item_id VARCHAR(40)     NOT NULL,
  count   INT UNSIGNED    NOT NULL DEFAULT 0,

  PRIMARY KEY (user_id, item_id),
  CONSTRAINT fk_inventory_user FOREIGN KEY (user_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- expeditions : 탐사
--   claimed_at 이 NULL 이면 아직 수령 전.
--
--   "수령 전 탐사는 유저당 1개"는 서버에서 이렇게 보장한다:
--     트랜잭션 안에서
--       SELECT ... FROM expeditions
--        WHERE user_id = ? AND claimed_at IS NULL FOR UPDATE;
--     결과가 있으면 EXPEDITION_IN_PROGRESS 로 거절, 없으면 INSERT.
-- ------------------------------------------------------------
CREATE TABLE expeditions (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  user_id    BIGINT UNSIGNED NOT NULL,
  area_id    VARCHAR(40)     NOT NULL,
  started_at DATETIME(3)     NOT NULL,
  end_at     DATETIME(3)     NOT NULL,
  claimed_at DATETIME(3)     NULL DEFAULT NULL,

  PRIMARY KEY (id),
  -- 진행 중인 탐사를 빠르게 찾기 위한 인덱스
  KEY idx_expeditions_active (user_id, claimed_at),
  KEY idx_expeditions_user (user_id, started_at),
  CONSTRAINT fk_expeditions_user FOREIGN KEY (user_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- village_likes : 마을 좋아요 (하루 1인 1회)
--   like_date 를 UTC 날짜로 저장하고 UNIQUE 로 묶어 중복을 DB가 막는다
-- ------------------------------------------------------------
CREATE TABLE village_likes (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  owner_id   BIGINT UNSIGNED NOT NULL,
  visitor_id BIGINT UNSIGNED NOT NULL,
  like_date  DATE            NOT NULL,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (id),
  UNIQUE KEY uq_likes_daily (owner_id, visitor_id, like_date),
  KEY idx_likes_owner (owner_id, created_at),
  CONSTRAINT fk_likes_owner FOREIGN KEY (owner_id)
    REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_likes_visitor FOREIGN KEY (visitor_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- guestbook : 방명록 한 줄
-- ------------------------------------------------------------
CREATE TABLE guestbook (
  id         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  owner_id   BIGINT UNSIGNED NOT NULL,
  writer_id  BIGINT UNSIGNED NOT NULL,
  message    VARCHAR(100)    NOT NULL,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (id),
  KEY idx_guestbook_owner (owner_id, created_at),
  CONSTRAINT fk_guestbook_owner FOREIGN KEY (owner_id)
    REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_guestbook_writer FOREIGN KEY (writer_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- schema_version : 스키마 버전 기록
--   변경 파일을 추가할 때마다 여기 한 줄씩 남겨 팀원 간 버전을 맞춘다
-- ------------------------------------------------------------
CREATE TABLE schema_version (
  version     INT          NOT NULL,
  description VARCHAR(200) NOT NULL,
  applied_at  DATETIME(3)  NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  PRIMARY KEY (version)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_version (version, description)
VALUES (1, '최초 스키마: users, villages, buildings, inventory, expeditions, village_likes, guestbook');
