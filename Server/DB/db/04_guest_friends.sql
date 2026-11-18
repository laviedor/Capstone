-- ============================================================
-- 04 : Guest 로그인, 친구, 1:1 채팅
--
--  - user_auth.password_hash : Guest 계정(provider = 'guest')의 비밀번호 해시
--                              Guest 의 login_key 는 소문자로 바꾼 아이디, 닉네임은 아이디 그대로
--  - friends                 : 친구 관계. 친구가 되면 양쪽에 한 줄씩 (A->B, B->A) 넣는다
--  - direct_messages         : 친구끼리의 1:1 채팅 (광장 채팅과 달리 DB 에 남는다)
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

ALTER TABLE user_auth ADD COLUMN password_hash VARCHAR(255) NULL AFTER login_key;


-- ------------------------------------------------------------
-- friends : 친구 관계 (서로 친구, 양쪽에 한 줄씩)
-- ------------------------------------------------------------
CREATE TABLE friends (
  user_id    BIGINT UNSIGNED NOT NULL,
  friend_id  BIGINT UNSIGNED NOT NULL,
  created_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (user_id, friend_id),
  KEY idx_friends_friend (friend_id),
  CONSTRAINT fk_friends_user FOREIGN KEY (user_id)
    REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_friends_friend FOREIGN KEY (friend_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- ------------------------------------------------------------
-- direct_messages : 1:1 채팅
--   read_at 이 NULL 이면 받는 사람이 아직 읽지 않은 메시지
-- ------------------------------------------------------------
CREATE TABLE direct_messages (
  id          BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  sender_id   BIGINT UNSIGNED NOT NULL,
  receiver_id BIGINT UNSIGNED NOT NULL,
  text        VARCHAR(200)    NOT NULL,
  created_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
  read_at     DATETIME(3)     NULL DEFAULT NULL,

  PRIMARY KEY (id),
  KEY idx_dm_pair (sender_id, receiver_id, id),
  KEY idx_dm_unread (receiver_id, sender_id, read_at),
  CONSTRAINT fk_dm_sender FOREIGN KEY (sender_id)
    REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_dm_receiver FOREIGN KEY (receiver_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


INSERT INTO schema_version (version, description)
VALUES (3, 'Guest 로그인: user_auth.password_hash / 친구: friends / 1:1 채팅: direct_messages');
