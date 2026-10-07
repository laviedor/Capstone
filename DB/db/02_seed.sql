-- ============================================================
-- 개발용 테스트 데이터
-- 광장이 비어 보이지 않도록 더미 마을 3개를 넣어둔다.
-- 실제 서비스 데이터가 아니므로 마음껏 지우고 다시 넣어도 된다.
-- ============================================================

SET NAMES utf8mb4;

INSERT INTO users (id, device_id, nickname, gold) VALUES
  (1, 'dev-device-mean',   '민성', 1200),
  (2, 'dev-device-hyobin', '효빈',  800),
  (3, 'dev-device-dongseon','동선', 1500),
  (4, 'dev-device-hyeongyu','현규', 1700);

INSERT INTO villages (owner_id, width, height, like_count) VALUES
  (1, 40, 8, 3),
  (2, 40, 8, 7),
  (3, 40, 8, 1),
  (4, 40, 8, 0);

INSERT INTO buildings (owner_id, building_id, x, y) VALUES
  (1, 'cottage',    3, 0),
  (1, 'flower_bed', 9, 0),
  (2, 'cottage',    5, 0),
  (2, 'well',      12, 0),
  (2, 'flower_bed',18, 0),
  (3, 'cottage',    2, 0);

INSERT INTO inventory (user_id, item_id, count) VALUES
  (1, 'wood', 34), (1, 'stone', 12),
  (2, 'wood', 20), (2, 'mushroom', 5),
  (3, 'wood', 60), (3, 'stone', 30);

-- 진행 중인 탐사 하나 (민성, 30분짜리)
INSERT INTO expeditions (user_id, area_id, started_at, end_at) VALUES
  (1, 'forest', UTC_TIMESTAMP(3), DATE_ADD(UTC_TIMESTAMP(3), INTERVAL 30 MINUTE));

INSERT INTO guestbook (owner_id, writer_id, message) VALUES
  (1, 2, '마을 예쁘게 꾸몄네요!'),
  (1, 3, '꽃밭 배치 참고할게요 🌼'),
  (2, 1, '우물 위치 좋다');
