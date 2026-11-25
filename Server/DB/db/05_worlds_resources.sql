-- ============================================================
-- 05 : 월드(광장 / 개인 마을), 건물 저장, 재화, 건물 생산
--
--  - buildings 를 서버가 실제로 쓰도록 바꾼다 (지금까지는 서버 메모리에만 있었음)
--      uid         : 네트워크 id (지은 클라이언트가 만든 문자열, 채팅 서버 메시지의 id)
--      village_id  : 건물이 있는 월드. 0 은 광장, 그 외에는 그 유저(id)의 마을
--      owner_id    : 지은 사람 (개인 마을에서는 항상 마을 주인)
--      x, y        : 기준 칸. 음수 칸도 있으므로 부호 있는 정수로 변경
--      produced_at : 건물 생산을 여기까지 정산했다는 시각 (서버 production.js)
--      같은 월드의 같은 칸에는 하나만 (uq_buildings_tile)
--  - 재화는 users.gold 와 inventory(wood, stone ...)를 그대로 쓴다. 지금 있는 유저에게 처음 재화를 준다
--    (새로 가입하는 유저는 서버가 가입할 때 준다: config.js STARTING_RESOURCES 와 같은 값)
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

ALTER TABLE buildings
  DROP INDEX uq_buildings_tile,
  ADD COLUMN uid VARCHAR(64) NULL AFTER building_uid,
  ADD COLUMN village_id BIGINT UNSIGNED NOT NULL DEFAULT 0 AFTER uid,
  MODIFY x INT NOT NULL,
  MODIFY y INT NOT NULL,
  ADD COLUMN produced_at DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3);

-- 지금까지 있던 건물(개발용 테스트 데이터)은 주인의 마을에 있는 것으로 한다
UPDATE buildings SET uid = CONCAT('seed-', building_uid), village_id = owner_id WHERE uid IS NULL;

ALTER TABLE buildings
  MODIFY uid VARCHAR(64) NOT NULL,
  ADD UNIQUE KEY uq_buildings_uid (uid),
  ADD UNIQUE KEY uq_buildings_tile (village_id, x, y);

-- 처음 재화: 나무 1000, 돌 1000, 골드 1000
UPDATE users SET gold = gold + 1000;
INSERT INTO inventory (user_id, item_id, count)
  SELECT id, 'wood', 1000 FROM users
  ON DUPLICATE KEY UPDATE count = count + 1000;
INSERT INTO inventory (user_id, item_id, count)
  SELECT id, 'stone', 1000 FROM users
  ON DUPLICATE KEY UPDATE count = count + 1000;

INSERT INTO schema_version (version, description)
VALUES (4, '월드/건물 저장: buildings.uid, village_id, produced_at, 좌표 부호 있는 정수 / 처음 재화 지급');
