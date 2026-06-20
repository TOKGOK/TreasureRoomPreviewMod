using System.Collections.Generic;
using UnityEngine;

namespace TreasureRoomPreviewMod
{
    /// <summary>
    /// 宝箱房透视 Mod
    /// 在宝箱房（GameTreasure）抽奖开始后，
    /// 将每个宝箱（862）的精灵直接替换为宝箱内对应能力的图标，
    /// 让玩家在推/打开宝箱前就能看到里面有什么能力。
    /// </summary>
    public class Main : SimpleModBehaviour
    {
        private const string ModVersion = "0.1.1";
        private const int ChestUnitID = 862;
        private const string LogPrefix = "[TreasureRoomPreview]";

        // 记录每个宝箱的原始精灵，用于还原
        private readonly Dictionary<UnitObject, Sprite> _originalSprites = new Dictionary<UnitObject, Sprite>();

        // 已处理过的宝箱集合（支持多轮刷新）
        private readonly HashSet<UnitObject> _processedChests = new HashSet<UnitObject>();

        // 标记当前是否在宝箱房
        private bool _inTreasureRoom;

        // 标记是否已订阅 OnTreasureOpen
        private bool _subscribedTreasureOpen;

        public override void OnModLoaded()
        {
            Debug.Log($"{LogPrefix} V{ModVersion} 已加载：宝箱房透视激活。");
            BattleObject.OnLevelStart += OnLevelStart;
        }

        public override void OnModUnloaded()
        {
            BattleObject.OnLevelStart -= OnLevelStart;
            UnsubscribeTreasureOpen();
            RestoreAllSprites();
            Debug.Log($"{LogPrefix} V{ModVersion} 已卸载。");
        }

        /// <summary>
        /// 关卡开始时：重置状态，如果是宝箱房则标记等待宝箱生成
        /// 同时确保已订阅 OnTreasureOpen（此时 BattleObject 实例已就绪）
        /// </summary>
        private void OnLevelStart(BattleObject bo)
        {
            RestoreAllSprites();
            _inTreasureRoom = bo.currentRoom == RoomType.GameTreasure;
            _processedChests.Clear();

            // 确保订阅开箱事件（BattleObject 实例此时一定存在）
            if (!_subscribedTreasureOpen)
            {
                bo.OnTreasureOpen += OnTreasureOpen;
                _subscribedTreasureOpen = true;
            }
        }

        /// <summary>
        /// 取消订阅 OnTreasureOpen
        /// </summary>
        private void UnsubscribeTreasureOpen()
        {
            if (_subscribedTreasureOpen)
            {
                SingletonData<BattleObject>.Instance.OnTreasureOpen -= OnTreasureOpen;
                _subscribedTreasureOpen = false;
            }
        }

        /// <summary>
        /// 每帧更新：在宝箱房中检测宝箱生成并替换为能力图标
        /// 支持多轮抽奖：每轮新生成的宝箱都会被检测到
        /// </summary>
        private void Update()
        {
            if (!_inTreasureRoom)
                return;

            var bo = SingletonData<BattleObject>.Instance;
            if (bo.currentRoom != RoomType.GameTreasure)
            {
                _inTreasureRoom = false;
                return;
            }

            // 查找所有宝箱单位（862）
            var chests = bo.GetUnit(ChestUnitID);
            if (chests == null || chests.Count == 0)
                return;

            // 替换尚未处理过的宝箱
            ReplaceChestSpritesWithAbilityIcons(chests);
        }

        /// <summary>
        /// 将尚未处理过的宝箱精灵替换为其内部能力的图标
        /// </summary>
        private void ReplaceChestSpritesWithAbilityIcons(List<UnitObject> chests)
        {
            int count = 0;
            foreach (var chest in chests)
            {
                if (chest == null || chest.hasDead || chest.unitNode == null)
                    continue;

                // 跳过已处理的宝箱
                if (_processedChests.Contains(chest))
                    continue;

                // 获取宝箱内能力 ID
                int abilityID = chest.abilityID;
                if (abilityID <= 0)
                    continue;

                // 获取能力配置和图标
                SkillConfig config = SkillConfigLoader.GetConfig(abilityID);
                if (config == null || config.icon == null)
                    continue;

                // 保存原始精灵用于还原
                if (chest.unitNode.unitSprite != null && !_originalSprites.ContainsKey(chest))
                {
                    _originalSprites[chest] = chest.unitNode.unitSprite.sprite;
                }

                // 直接替换宝箱精灵为能力图标
                chest.unitNode.unitSprite.sprite = config.icon;
                _processedChests.Add(chest);
                count++;
            }

            if (count > 0)
                Debug.Log($"{LogPrefix} 已将 {count} 个宝箱替换为能力图标。");
        }

        /// <summary>
        /// 宝箱被打开时：还原对应宝箱的原始精灵并移除跟踪
        /// </summary>
        private void OnTreasureOpen(UnitObject chest)
        {
            _processedChests.Remove(chest);
            RestoreSprite(chest);
        }

        /// <summary>
        /// 还原单个宝箱的精灵
        /// </summary>
        private void RestoreSprite(UnitObject chest)
        {
            if (_originalSprites.TryGetValue(chest, out var originalSprite))
            {
                _originalSprites.Remove(chest);
                if (chest != null && !chest.hasDead && chest.unitNode?.unitSprite != null)
                {
                    chest.unitNode.unitSprite.sprite = originalSprite;
                }
            }
        }

        /// <summary>
        /// 还原所有宝箱的精灵并清理记录
        /// </summary>
        private void RestoreAllSprites()
        {
            foreach (var kvp in _originalSprites)
            {
                var chest = kvp.Key;
                var originalSprite = kvp.Value;
                if (chest != null && !chest.hasDead && chest.unitNode?.unitSprite != null)
                {
                    chest.unitNode.unitSprite.sprite = originalSprite;
                }
            }
            _originalSprites.Clear();
            _processedChests.Clear();
            _inTreasureRoom = false;
        }
    }
}
