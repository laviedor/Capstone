-- ============================================================
-- 03 : Steam 로그인
--
--  - user_auth : 로그인 수단 -> 유저 연결. 지금은 provider = 'steam' 만 사용
--  - sessions  : 로그인 후 발급한 세션 토큰. 토큰 원문은 저장하지 않고 SHA-256 해시만 저장
--  - users.device_id 를 NULL 허용으로 변경 (Steam 으로 만든 계정에는 device_id 가 없음)
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

ALTER TABLE users MODIFY device_id VARCHAR(128) NULL;


-- ------------------------------------------------------------
-- user_auth : 로그인 수단 (유저당 provider 별 1개)
--   steam 의 login_key 는 SteamID64 (17자리 숫자).
--   JS Number 로는 정확히 담을 수 없으므로 서버에서는 항상 문자열로 다룬다
-- ------------------------------------------------------------
CREATE TABLE user_auth (
  provider   VARCHAR(16)     NOT NULL,
  login_key  VARCHAR(128)    NOT NULL,
  user_id    BIGINT UNSIGNED NOT NULL,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (provider, login_key),
  UNIQUE KEY uq_auth_user_provider (user_id, provider),
  CONSTRAINT fk_auth_user FOREIGN KEY (user_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- sessions : 로그인 세션
--   DB 가 털려도 토큰을 바로 쓸 수 없도록 SHA-256(토큰) 만 저장한다
-- ------------------------------------------------------------
CREATE TABLE sessions (
  token_hash CHAR(64)        NOT NULL,
  user_id    BIGINT UNSIGNED NOT NULL,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  expires_at DATETIME(3)     NOT NULL,

  PRIMARY KEY (token_hash),
  KEY idx_sessions_user (user_id),
  CONSTRAINT fk_sessions_user FOREIGN KEY (user_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


INSERT INTO schema_version (version, description)
VALUES (2, 'Steam 로그인: user_auth, sessions 추가, users.device_id NULL 허용');
