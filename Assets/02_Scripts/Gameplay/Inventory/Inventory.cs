using System;
using System.Collections.Generic;
using HamPocha.Audio;
using HamPocha.Data;
using UnityEngine;

namespace HamPocha.Gameplay
{
    public enum IngredientType
    {
        Carrot = 0,
        Bread = 1,
        Tomato = 2,
        Meat = 3,
        Corn = 4,
        Lettuce = 5,
        Mushroom = 6,
        Onion = 7,
        Shrimp = 8,
        Paprika = 9,
        Sausage = 10
    }

    [Serializable]
    public class IngredientSlot
    {
        [SerializeField] private IngredientType _ingredient;
        [SerializeField] private int _count;

        [SerializeField] private bool _isFood;
        [SerializeField] private RecipeId _recipe;
        public bool IsFood { get { return _isFood; } }
        public RecipeId Recipe { get { return _recipe; } }
        public int ItemKey { get { return _isFood ? 5 + (int)_recipe : Inventory.IngredientKey(_ingredient); } }

        public void Set(int itemKey, int count)
        {
            _isFood = Inventory.IsFoodKey(itemKey);
            _recipe = _isFood ? (RecipeId)(itemKey - 5) : RecipeId.CarrotSoup;
            _ingredient = _isFood ? IngredientType.Carrot : Inventory.KeyIngredient(itemKey);
            _count = count;
        }

        public IngredientType Ingredient { get { return _ingredient; } }

        public int Count { get { return _count; } }

    }

    // 가방과 모든 포차 공용창고를 구분한다. 입고하지 못한 재료는 가방에 남긴다.
    // 이름 변경 전 직렬화 타입 정보를 유지한다.
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "AiMobileProject.Gameplay", null, "ResourceInventory")]
    public class Inventory : MonoBehaviour
    {
        public const int FridgeLimit = 1000;
        // 실내 주방과 초기 튜토리얼에서 쓰는 재료 수다. 야외 재료는 인벤토리에서 함께 보관한다.
        public const int IngredientCount = 6;
        public const int IngredientTypeCount = 11;
        public const int StorageSlotCount = IngredientCount;
        public const int LettuceKey = 12;
        public const int IndoorItemKeyCount = LettuceKey + 1;
        public const int ItemKeyCount = IngredientTypeCount + 7;

        // 구버전의 음식 키 5~11과 양상추 12를 보존한다.
        public static bool IsFoodKey(int key) { return key >= 5 && key < LettuceKey; }
        public static bool IsIngredientKey(int key) { return key >= 0 && key < ItemKeyCount && !IsFoodKey(key); }
        public static int IngredientKey(IngredientType ingredient) { return ingredient >= IngredientType.Lettuce ? (int)ingredient + 7 : (int)ingredient; }
        public static IngredientType KeyIngredient(int key) { return (IngredientType)(key >= LettuceKey ? key - 7 : key); }

        public static int IngredientForest(IngredientType ingredient)
        {
            if (ingredient == IngredientType.Paprika || ingredient == IngredientType.Sausage) return 3;
            if (ingredient == IngredientType.Corn || ingredient == IngredientType.Lettuce ||
                ingredient == IngredientType.Mushroom || ingredient == IngredientType.Onion || ingredient == IngredientType.Shrimp) return 2;
            if (ingredient == IngredientType.Tomato || ingredient == IngredientType.Meat) return 1;
            return 0;
        }

        public static bool IsHuntDrop(IngredientType ingredient)
        {
            return ingredient == IngredientType.Bread || ingredient == IngredientType.Meat ||
                ingredient == IngredientType.Shrimp || ingredient == IngredientType.Sausage;
        }

        private const int FoodStackLimit = 100;
        [SerializeField] private IngredientSlot[] _slots = Array.Empty<IngredientSlot>();
        [SerializeField] private int[] _foods = new int[7];
        private readonly long[] _bag = new long[IngredientTypeCount];
        private readonly int[] _fridge = new int[IngredientTypeCount];
        private readonly IngredientSlot[] _storedFood = CreateFoodSlots();

        public int Version { get; private set; }

        private static IngredientSlot[] CreateFoodSlots()
        {
            var slots = new IngredientSlot[StorageSlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = new IngredientSlot();
            return slots;
        }

        private void Awake()
        {
            if (_foods.Length != 7) Array.Resize(ref _foods, 7);
            foreach (IngredientSlot slot in _slots)
                if (slot != null && !slot.IsFood && slot.Count > 0)
                    _bag[(int)slot.Ingredient] += slot.Count;
        }

        public long GetCount(IngredientType ingredient) => _bag[(int)ingredient];

        internal void ApplyMailboxBag(long[] bag)
        {
            Array.Copy(bag, _bag, _bag.Length);
            Version++;
        }

        public int GetStorageCount(IngredientType ingredient) => _fridge[(int)ingredient];

        public int GetTotalCount(IngredientType ingredient)
        {
            long bag = GetCount(ingredient);
            int fridge = GetStorageCount(ingredient);
            return bag >= int.MaxValue - fridge ? int.MaxValue : (int)bag + fridge;
        }

        public bool TryAdd(IngredientType ingredient, int amount)
        {
            int index = (int)ingredient;
            if (index < 0 || index >= IngredientTypeCount || amount <= 0 || _bag[index] > long.MaxValue - amount)
            {
                Debug.LogWarning($"[Gameplay] frame={Time.frameCount} event=ingredient_add result=rejected reason=invalid_ingredient_or_amount ingredient={ingredient} amount={amount}", this);
                return false;
            }

            _bag[index] += amount;
            Version++;
            if (Debug.isDebugBuild)
                Debug.Log($"[Gameplay] frame={Time.frameCount} event=ingredient_add result=completed ingredient={ingredient} amount={amount} total={_bag[index]}", this);
            return true;
        }

        internal int ApplyForestLoss(int[] losses)
        {
            int lost = 0;
            for (int i = 0; i < _bag.Length; i++)
            {
                int amount = (int)Math.Min(_bag[i], losses[i]);
                _bag[i] -= amount;
                lost += amount;
            }
            if (lost > 0) Version++;
            return lost;
        }

        public int DepositAll()
        {
            int moved = 0;
            for (int i = 0; i < IngredientTypeCount; i++)
            {
                int amount = (int)Math.Min(_bag[i], FridgeLimit - _fridge[i]);
                _fridge[i] += amount;
                _bag[i] -= amount;
                moved += amount;
            }
            if (moved > 0)
            {
                Version++;
                Sfx.Play(SfxCue.Store);
            }
            Debug.Log($"[Gameplay] frame={Time.frameCount} event=storage_deposit result=completed moved={moved}", this);
            return moved;
        }

        public bool TryPutFridge(IngredientType ingredient)
        {
            int index = (int)ingredient;
            if (_fridge[index] >= FridgeLimit) return false;
            _fridge[index]++;
            Version++;
            return true;
        }

        public bool TryReturnKitchenIngredient(IngredientType ingredient, int count)
        {
            int index = (int)ingredient;
            int stored = Math.Min(count, FridgeLimit - _fridge[index]);
            int remainder = count - stored;
            if (_bag[index] > long.MaxValue - remainder)
            {
                Debug.LogWarning($"[Save] event=kitchen_ingredient_restore result=rejected reason=quantity_overflow ingredient={ingredient} amount={count}", this);
                return false;
            }
            _fridge[index] += stored;
            _bag[index] += remainder;
            Version++;
            return true;
        }

        internal bool TryRefundFridgeRecipe(RecipeIngredient[] ingredients, int portions)
        {
            // 두 재료 중 하나를 돌려줄 수 없을 때 다른 재료부터 변경하지 않는다.
            foreach (RecipeIngredient item in ingredients)
            {
                int index = (int)item.Ingredient;
                int count = item.Count * portions;
                int overflow = Math.Max(0, count - (FridgeLimit - _fridge[index]));
                if (_bag[index] > long.MaxValue - overflow) return false;
            }

            foreach (RecipeIngredient item in ingredients)
            {
                TryReturnKitchenIngredient(item.Ingredient, item.Count * portions);
            }

            return true;
        }

        public bool TryTakeFridge(IngredientType ingredient)
        {
            int index = (int)ingredient;
            if (_fridge[index] == 0) return false;
            _fridge[index]--;
            Version++;
            return true;
        }

        public bool HasFridgeRecipe(RecipeData recipe)
        {
            return HasFridgeIngredients(recipe.Ingredients);
        }

        public bool HasFridgeIngredients(RecipeIngredient[] ingredients)
        {
            foreach (RecipeIngredient item in ingredients)
                if (GetStorageCount(item.Ingredient) < item.Count) return false;
            return true;
        }

        public bool TryConsumeFridgeRecipe(RecipeData recipe)
        {
            return TryConsumeFridgeIngredients(recipe.Ingredients);
        }

        public bool TryConsumeFridgeIngredients(RecipeIngredient[] ingredients)
        {
            if (!HasFridgeIngredients(ingredients)) return false;
            foreach (RecipeIngredient item in ingredients)
                _fridge[(int)item.Ingredient] -= item.Count;
            Version++;
            return true;
        }

        public bool TryConsumeRecipe(RecipeData recipe)
        {
            // 필요한 재료를 모두 확인한 후 냉장고부터 차감한다.
            foreach (RecipeIngredient item in recipe.Ingredients)
            {
                if (GetTotalCount(item.Ingredient) >= item.Count) continue;
                Debug.LogWarning($"[Gameplay] frame={Time.frameCount} event=recipe_consume result=rejected reason=no_stock recipe={recipe.Id} ingredient={item.Ingredient}", this);
                return false;
            }

            foreach (RecipeIngredient item in recipe.Ingredients)
            {
                int index = (int)item.Ingredient;
                int stored = Math.Min(_fridge[index], item.Count);
                _fridge[index] -= stored;
                _bag[index] -= item.Count - stored;
            }

            Version++;
            Debug.Log($"[Gameplay] frame={Time.frameCount} event=recipe_consume result=completed recipe={recipe.Id}", this);
            return true;
        }


        public static string GetIngredientName(IngredientType ingredient)
        {
            switch (ingredient)
            {
                case IngredientType.Carrot: return "당근";
                case IngredientType.Bread: return "빵";
                case IngredientType.Tomato: return "토마토";
                case IngredientType.Meat: return "고기";
                case IngredientType.Corn: return "옥수수";
                case IngredientType.Lettuce: return "양상추";
                case IngredientType.Mushroom: return "버섯";
                case IngredientType.Onion: return "양파";
                case IngredientType.Shrimp: return "새우";
                case IngredientType.Paprika: return "파프리카";
                case IngredientType.Sausage: return "소시지";
                default: return "재료";
            }
        }

        public int GetFoodCount(RecipeId recipe)
        {
            int count = _foods[(int)recipe];
            foreach (IngredientSlot slot in _storedFood) if (slot.IsFood && slot.Recipe == recipe) count += slot.Count;
            return count;
        }

        public void AddFood(RecipeId recipe)
        {
            _foods[(int)recipe]++;
            Version++;

            if (Debug.isDebugBuild)
            {
                Debug.Log($"[Gameplay] [frame={Time.frameCount}] 완성 음식 보관: 추가. event=food_store result=added recipe={recipe} count={GetFoodCount(recipe)}", this);
            }
        }

        public bool TryTakeFood(RecipeId recipe)
        {
            if (GetFoodCount(recipe) <= 0)
            {
                return false;
            }

            foreach (IngredientSlot slot in _storedFood)
            {
                if (!slot.IsFood || slot.Recipe != recipe || slot.Count <= 0) continue;
                slot.Set(slot.ItemKey, slot.Count - 1);
                Version++;
                return true;
            }
            _foods[(int)recipe]--;
            Version++;

            return true;
        }

        // 새 종료 음식은 실제5칸을 사용한다. 구버전 회수분(_foods)과 구분한다.
        public bool TryStoreFood(RecipeId recipe)
        {
            int key = 5 + (int)recipe;
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (IngredientSlot slot in _storedFood)
                {
                    if (pass == 0 ? slot.Count <= 0 || slot.ItemKey != key || slot.Count >= FoodStackLimit : slot.Count != 0) continue;
                    slot.Set(key, slot.Count + 1);
                    Version++;
                    return true;
                }
            }
            return false;
        }

        public InventorySaveData Capture()
        {
            var data = new InventorySaveData
            {
                Bag = (long[])_bag.Clone(),
                Fridge = (int[])_fridge.Clone(),
                Ingredients = new List<IngredientSaveData>(),
                Storage = new List<IngredientSaveData>(),
                Foods = new List<FoodSaveData>()
            };
            foreach (IngredientSlot slot in _storedFood)
                data.Storage.Add(new IngredientSaveData { IsFood = slot.IsFood, Recipe = slot.Recipe, Count = slot.Count });
            for (int i = 0; i < _foods.Length; i++)
                data.Foods.Add(new FoodSaveData { Recipe = (RecipeId)i, Count = _foods[i] });
            return data;
        }

        public bool CanRestore(InventorySaveData data)
        {
            if (data.Storage == null || (data.Storage.Count != StorageSlotCount && data.Storage.Count != 5) ||
                (data.Foods.Count != 2 && data.Foods.Count != 7)) return false;

            for (int i = 0; i < data.Foods.Count; i++)
                if ((int)data.Foods[i].Recipe != i) return false;
            return true;
        }

        public void Restore(InventorySaveData data)
        {
            Array.Clear(_bag, 0, _bag.Length);
            Array.Clear(_fridge, 0, _fridge.Length);
            Array.Clear(_foods, 0, _foods.Length);

            bool hasMaterialCounts = data.Bag?.Length == IngredientTypeCount && data.Fridge?.Length == IngredientTypeCount;
            if (hasMaterialCounts)
            {
                Array.Copy(data.Bag, _bag, _bag.Length);
                Array.Copy(data.Fridge, _fridge, _fridge.Length);
            }
            else
            {
                foreach (IngredientSaveData slot in data.Ingredients) _bag[(int)slot.Ingredient] += slot.Count;
                foreach (IngredientSaveData slot in data.BagOverflow) _bag[(int)slot.Ingredient] += slot.Count;
                foreach (IngredientSaveData slot in data.StorageOverflow) RestoreStoredIngredient(slot);
            }

            for (int i = 0; i < _storedFood.Length; i++)
            {
                _storedFood[i] = new IngredientSlot();
                if (i >= data.Storage.Count) continue;
                IngredientSaveData slot = data.Storage[i];
                if (slot.IsFood) _storedFood[i].Set(5 + (int)slot.Recipe, slot.Count);
                else if (!hasMaterialCounts) RestoreStoredIngredient(slot);
            }
            for (int i = 0; i < data.Foods.Count; i++) _foods[i] = data.Foods[i].Count;

            Version++;
            Debug.Log($"[Save] frame={Time.frameCount} event=inventory_restore result=success migrated={!hasMaterialCounts}", this);
        }

        private void RestoreStoredIngredient(IngredientSaveData slot)
        {
            int index = (int)slot.Ingredient;
            int stored = Math.Min(slot.Count, FridgeLimit - _fridge[index]);
            _fridge[index] += stored;
            _bag[index] += slot.Count - stored;
        }
    }
}
