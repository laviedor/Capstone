-- ============================================================
-- 07 : 건물 수확
--
--  건물이 자원을 내는 방식이 "주기마다 자동 생산"에서 "수확 버튼을 눌러 받기"로 바뀌었다
--  (클라이언트 BuildingHarvest, 서버 harvest.js)
--
--  - buildings.produced_at -> harvested_at
--      지금까지: 생산을 여기까지 정산했다는 시각
--      이제부터: 마지막으로 수확한 시각 (수확한 적이 없으면 지은 시각). 여기서 수확 주기가 지나면 수확할 수 있다
--      있던 값은 그대로 둔다 (오래된 건물은 바로 수확할 수 있는 상태가 된다)
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

ALTER TABLE buildings
  RENAME COLUMN produced_at TO harvested_at;

INSERT INTO schema_version (version, description)
VALUES (6, '건물 수확: buildings.produced_at -> harvested_at');
