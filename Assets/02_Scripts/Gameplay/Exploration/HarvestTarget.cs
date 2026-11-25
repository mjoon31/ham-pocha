using UnityEngine;
using HamPocha.Core;
using HamPocha.Audio;
using HamPocha.Data;
using HamPocha.UI;

namespace HamPocha.Gameplay
{
    // 이름 변경 전 직렬화 타입 정보를 유지한다.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "AiMobileProject.Gameplay", null, "ForestHarvestTarget")]
    public class HarvestTarget : MonoBehaviour
    {
        [SerializeField] private IngredientType _ingredient;
        [SerializeField, Min(1)] private int _hitPoints = 1;
        [SerializeField] private bool _isMonster;
        [SerializeField] private string _monsterName;
        [SerializeField, Min(1)] private int _yieldCount = 1;
        [SerializeField, Min(0)] private int _tutorialYieldCount;
        [SerializeField] private bool _nightOnly;
        [SerializeField] private bool _hideDuringTutorial;
        [SerializeField] private GameObject _selectionMarker;
        [SerializeField] private ParticleSystem _harvestDust;

        private bool _isCollected;
        private int _initialHitPoints;
        private int _legacyHitCount;
        private Vector3 _spawnPosition;
        private bool _hasSpawnState;
        private bool _useTutorialYield;
        private WorldClock _clock;
        private int _dayYield;
        private int _harvestedAmount;
        private bool _noHarvestReward;
        private double _nextMonsterHitTime;

        private void Awake()
        {
            InitializeSpawn();
            if (_selectionMarker != null) _selectionMarker.SetActive(false);
        }

        private void InitializeSpawn()
        {
            if (_hasSpawnState)
            {
                return;
            }

            _legacyHitCount = _hitPoints;
            _initialHitPoints = _isMonster ? ForestCombatRules.MonsterMaxHealth(ForestCombatRules.Tier(transform)) : _hitPoints;
            _hitPoints = _initialHitPoints;
            _spawnPosition = transform.position;
            _hasSpawnState = true;
        }

        public void ResetForEntry(Vector3 spawnPosition)
        {
            InitializeSpawn();
            _spawnPosition = spawnPosition;
            ResetForEntry();
        }

        public void ResetForEntry()
        {
            // 부모 OnEnable이 자식 Awake보다 먼저 실행돼도 최초 배치를 보존한다.
            InitializeSpawn();
            _hitPoints = _initialHitPoints;
            _isCollected = false;
            _harvestedAmount = 0;
            _noHarvestReward = false;
            _nextMonsterHitTime = 0d;

            Rigidbody body = GetComponent<Rigidbody>();

            if (body != null)
            {
                body.position = _spawnPosition;

                if (!body.isKinematic)
                {
                    body.velocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            transform.position = _spawnPosition;
            RefreshAvailability();

            Monster monster = GetComponent<Monster>();

            if (monster != null)
            {
                monster.ResetForEntry();
            }
        }

        public string ActionLabel
        {
            get
            {
                if (_isMonster)
                {
                    return MonsterLabel + " 사냥 (체력 " + _hitPoints + ") | +" + YieldCount;
                }

                return Inventory.GetIngredientName(_ingredient) + " 채집 +" + YieldCount;
            }
        }

        private string MonsterLabel
        {
            get
            {
                return string.IsNullOrEmpty(_monsterName) ? Inventory.GetIngredientName(_ingredient) + " 몽실이" : _monsterName;
            }
        }

        public IngredientType Ingredient { get { return _ingredient; } }
        public int YieldCount => _useTutorialYield ? (HiddenForTutorial ? 0 : BaseYield) :
            _clock != null && _clock.IsDaytime ? _dayYield : _yieldCount;
        internal int RegularYield => _yieldCount;
        private int BaseYield => _useTutorialYield && _tutorialYieldCount > 0 ? _tutorialYieldCount : _yieldCount;
        internal int HarvestedAmount => _noHarvestReward ? 0 : _harvestedAmount > 0 ? _harvestedAmount : BaseYield;
        internal bool NightOnly => _nightOnly;
        internal bool HiddenForTutorial => _useTutorialYield && _hideDuringTutorial;
        public bool IsCollected { get { return _isCollected; } }
        internal bool IsMonster => _isMonster;
        internal int HitPoints => _hitPoints;
        internal int MaxHitPoints => _initialHitPoints;

        public void SetHarvestYield(bool useTutorialYield, WorldClock clock, int dayYield)
        {
            _useTutorialYield = useTutorialYield;
            _clock = clock;
            _dayYield = dayYield;
        }

        public bool IsAvailable
        {
            get { return !_isCollected && gameObject.activeInHierarchy && YieldCount > 0; }
        }

        internal void RefreshAvailability()
        {
            gameObject.SetActive(!_isCollected && YieldCount > 0);
        }

        public Transform OutlineTarget
        {
            get
            {
                Transform visual = _isMonster ? null : transform.Find("Visual");
                return visual != null ? visual : transform;
            }
        }

        public TargetSaveData Capture()
        {
            InitializeSpawn();
            return new TargetSaveData { Id = SaveKey.Get(this), Position = transform.position,
                SpawnPosition = _spawnPosition, HitPoints = _hitPoints,
                MaxHitPoints = _isMonster ? _initialHitPoints : 0, IsCollected = _isCollected,
                IsActive = !_isCollected, HarvestedAmount = _harvestedAmount, NoHarvestReward = _noHarvestReward };
        }

        public void Restore(TargetSaveData data)
        {
            InitializeSpawn();
            _spawnPosition = data.SpawnPosition;
            _hitPoints = RestoreHitPoints(data);
            _isCollected = data.IsCollected;
            _harvestedAmount = data.HarvestedAmount;
            _noHarvestReward = data.NoHarvestReward;
            _nextMonsterHitTime = 0d;
            transform.position = data.Position;
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null) body.position = data.Position;

            RefreshAvailability();
            Monster monster = GetComponent<Monster>();
            if (monster != null) monster.RestorePatrol(data.SpawnPosition);
        }

        public bool CanRestore(TargetSaveData data)
        {
            InitializeSpawn();
            int savedMax = _isMonster && data.MaxHitPoints == 0 ? _legacyHitCount : _initialHitPoints;
            return data.HitPoints >= 0 && data.HitPoints <= savedMax &&
                (data.MaxHitPoints == 0 || _isMonster && data.MaxHitPoints == _initialHitPoints) &&
                data.IsCollected == (data.HitPoints == 0) &&
                data.IsActive == !data.IsCollected;
        }

        private int RestoreHitPoints(TargetSaveData data)
        {
            if (!_isMonster || data.MaxHitPoints > 0 || data.HitPoints == 0) return data.HitPoints;
            int priorHits = Mathf.Clamp(_legacyHitCount - data.HitPoints, 0, _legacyHitCount);
            return Mathf.Max(1, _initialHitPoints - priorHits * ForestCombatRules.PlayerAttack);
        }

        public bool TryHarvest(Inventory inventory, out string message, int playerLevel = 1)
        {
            if (!IsAvailable || inventory == null)
            {
                message = "지금은 이 대상을 사용할 수 없습니다.";
                Debug.LogWarning($"[Gameplay] [frame={Time.frameCount}] 채집 및 보상 지급: 거부. 대상 또는 인벤토리를 사용할 수 없습니다. event=harvest result=rejected reason=target_or_inventory_unavailable target={name}", this);

                return false;
            }

            if (_isMonster && Time.timeAsDouble < _nextMonsterHitTime)
            {
                message = "아직 다시 공격할 수 없습니다.";
                if (Debug.isDebugBuild)
                    Debug.Log($"[Gameplay] event=hunt result=rejected reason=target_cooldown target={name}", this);
                return false;
            }

            int damage = _isMonster ? ForestCombatRules.PlayerAttackAtLevel(playerLevel) : 1;
            if (_hitPoints > damage)
            {
                _hitPoints -= damage;
                if (_isMonster)
                {
                    _nextMonsterHitTime = Time.timeAsDouble + ForestCombatRules.MonsterHitInterval(ForestCombatRules.Tier(transform));
                    if (TryGetComponent(out Monster monster)) monster.PlayHitMotion();
                    ForestFeedbackHud.ShowDamage(transform, damage, player: false);
                }
                message = MonsterLabel + " 타격 · 남은 체력 " + _hitPoints;

                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[Gameplay] [frame={Time.frameCount}] 몬스터 공격: 타격. event=hunt result=hit target={name} hp={_hitPoints}", this);
                }

                return true;
            }

            // 재료 지급에 성공한 뒤에만 대상을 제거한다.
            int amount = YieldCount;
            if (!inventory.TryAdd(_ingredient, amount))
            {
                message = "재료를 받을 수 없어요. 재료는 이 자리에 남아요";

                return false;
            }

            if (_isMonster) ForestFeedbackHud.ShowDamage(transform, _hitPoints, player: false);
            _hitPoints = 0;
            _isCollected = true;
            _harvestedAmount = amount;
            message = Inventory.GetIngredientName(_ingredient) + " +" + amount + "개 획득";
            ForestFeedbackHud.ShowIngredient(_ingredient, amount);
            Sfx.Play(SfxCue.Harvest);
            if (Application.isPlaying && !_isMonster && _harvestDust != null)
            {
                PlayHarvestDust();
            }

            if (Application.isPlaying && _isMonster && TryGetComponent(out Monster caughtMonster))
                caughtMonster.PlayCaptureDust();

            gameObject.SetActive(false);

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 채집 및 보상 지급: 완료. event=harvest result=completed target={name} ingredient={_ingredient} amount={amount} monster={_isMonster}", this);
            }

            return true;
        }

        private void PlayHarvestDust()
        {
            Renderer model = OutlineTarget.GetComponentInChildren<Renderer>();
            Bounds bounds = model != null ? model.bounds : new Bounds(transform.position, Vector3.one * 0.3f);
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

            // 대상이 꺼진 뒤에도 같은 숲에서 흙먼지가 한 번 재생된다.
            ParticleSystem dust = Instantiate(_harvestDust, bounds.center, Quaternion.identity, transform.parent);
            if (transform.parent == null && dust.gameObject.scene != gameObject.scene)
            {
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(dust.gameObject, gameObject.scene);
            }

            dust.Play();
            var puff = new ParticleSystem.EmitParams();
            puff.startLifetime = 0.26f;
            puff.startSize = diameter * 1.35f;
            puff.velocity = Vector3.up * 0.38f;
            dust.Emit(puff, 1);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3f;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                puff.position = direction * (diameter * 0.22f) - Vector3.up * (bounds.extents.y * 0.45f);
                puff.velocity = direction * (diameter * 0.90f) + Vector3.up * 0.45f;
                puff.startSize = diameter * 0.57f;
                puff.startLifetime = 0.31f + (i % 3) * 0.04f;
                dust.Emit(puff, 1);
            }

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] event=harvest_dust result=played target={name}", this);
            }
        }
    }
}
