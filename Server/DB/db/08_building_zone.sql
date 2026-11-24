-- ============================================================
-- 08 : 건물이 놓인 구역 (마을 / 집 안)
--
--  - buildings.zone : '' 마을, 'house' 마을 주인 집 안 (월드 안의 구역, chatProtocol 의 zone 과 같은 값)
--      집 안에는 장식과 특수 건물만 놓는다 (서버 buildings.js 가 검사)
--      집 안과 마을은 서로 다른 공간이므로 같은 칸 중복 금지(uq_buildings_tile)도 구역별로 따로 본다
--  - 지금까지 있던 건물은 모두 마을에 있는 것으로 한다
--
-- 이미 만들어진 DB 에는 자동으로 적용되지 않으므로 직접 한 번 실행한다
-- ============================================================

SET NAMES utf8mb4;

ALTER TABLE buildings
  ADD COLUMN zone VARCHAR(16) NOT NULL DEFAULT '' AFTER village_id,
  DROP INDEX uq_buildings_tile,
  ADD UNIQUE KEY uq_buildings_tile (village_id, zone, x, y);

INSERT INTO schema_version (version, description)
VALUES (7, '건물 구역: buildings.zone (마을 / 집 안), 같은 칸 중복 금지를 구역별로');
