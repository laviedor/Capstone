-- ============================================================
-- 06 : 친구 요청
--
--  - friend_requests : 친구 요청 (보낸 사람 -> 받은 사람 한 줄)
--                      받은 사람이 수락하면 friends 에 양쪽 줄이 생기고 요청은 지워진다
--                      거절하거나 보낸 사람이 취소하면 요청만 지워진다
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

CREATE TABLE friend_requests (
  sender_id   BIGINT UNSIGNED NOT NULL,
  receiver_id BIGINT UNSIGNED NOT NULL,
  created_at  DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3),

  PRIMARY KEY (sender_id, receiver_id),
  KEY idx_friend_requests_receiver (receiver_id),
  CONSTRAINT fk_friend_requests_sender FOREIGN KEY (sender_id)
    REFERENCES users(id) ON DELETE CASCADE,
  CONSTRAINT fk_friend_requests_receiver FOREIGN KEY (receiver_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


INSERT INTO schema_version (version, description)
VALUES (5, '친구 요청: friend_requests');
