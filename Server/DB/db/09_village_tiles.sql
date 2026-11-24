-- ============================================================
-- 09 : 마을 바닥 타일 (삽으로 바꾼 칸)
--
--  - village_tiles : 개인 마을(village_id = 마을 주인 유저 id)의 바닥 칸 중 잔디가 아닌 칸
--      x, y : 바닥 타일맵의 칸 좌표, tile : 타일 종류 ('dirt' 흙)
--      잔디(기본 바닥)로 되돌린 칸은 지운다. 마을 주인이 탈퇴하면 함께 지워진다
--  - 삽 등 제작한 도구는 inventory 에 item_id 'shovel' 로 들어간다 (새 테이블 없음)
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

CREATE TABLE village_tiles (
  village_id BIGINT UNSIGNED NOT NULL,
  x          SMALLINT        NOT NULL,
  y          SMALLINT        NOT NULL,
  tile       VARCHAR(16)     NOT NULL,
  updated_at DATETIME(3)     NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),

  PRIMARY KEY (village_id, x, y),
  CONSTRAINT fk_village_tiles_owner FOREIGN KEY (village_id)
    REFERENCES users(id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO schema_version (version, description)
VALUES (8, '마을 바닥 타일: village_tiles (삽으로 바꾼 칸)');
