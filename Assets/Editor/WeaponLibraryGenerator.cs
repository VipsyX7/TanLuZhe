using UnityEditor;
using UnityEngine;

namespace TanLuZhe.EditorTools
{
    /// <summary>
    /// Creates the weapon assets (ScriptableObjects) used by the demo level. Re-runnable: it
    /// updates the existing assets in place so tuning survives a rebuild.
    /// </summary>
    public static class WeaponLibraryGenerator
    {
        public const string WeaponsDir = "Assets/Settings/Weapons/";
        public const string ArtDir = "Assets/Art/Generated/";

        public static readonly string[] Names = { "Sword", "Spear", "Hammer", "Bow", "Wand" };

        [MenuItem("TanLuZhe/4. Generate Weapon Assets", false, 4)]
        public static void GenerateAll()
        {
            ProjectSetup.EnsureFolder(WeaponsDir);

            // ---- melee -------------------------------------------------------
            WeaponDefinition sword = Create("Sword");
            sword.displayName = "Sword";
            sword.description = "Balanced melee blade. Reliable damage, quick recovery.";
            sword.kind = WeaponKind.Melee;
            sword.damage = 28f;
            sword.cooldown = 0.42f;
            sword.knockback = 11f;
            sword.hitShake = 0.22f;
            sword.meleeSize = new Vector2(1.8f, 1.4f);
            sword.meleeReach = 0.85f;
            sword.holdDistance = 0.5f;
            sword.holdScale = 0.85f;
            sword.holdAngle = 0f;
            sword.tint = new Color(0.92f, 0.96f, 1f);
            SetSprites(sword, "weapon_sword");

            WeaponDefinition spear = Create("Spear");
            spear.displayName = "Spear";
            spear.description = "Long reach, fast jabs, light knockback.";
            spear.kind = WeaponKind.Melee;
            spear.damage = 20f;
            spear.cooldown = 0.3f;
            spear.knockback = 7f;
            spear.hitShake = 0.16f;
            spear.meleeSize = new Vector2(2.6f, 1f);
            spear.meleeReach = 1.3f;
            spear.holdDistance = 0.5f;
            spear.holdScale = 0.8f;
            spear.tint = new Color(0.95f, 0.9f, 0.8f);
            SetSprites(spear, "weapon_spear");

            WeaponDefinition hammer = Create("Hammer");
            hammer.displayName = "Hammer";
            hammer.description = "Slow, huge damage, sends enemies flying.";
            hammer.kind = WeaponKind.Melee;
            hammer.damage = 55f;
            hammer.cooldown = 0.95f;
            hammer.knockback = 22f;
            hammer.hitShake = 0.55f;
            hammer.meleeSize = new Vector2(2.2f, 2f);
            hammer.meleeReach = 0.95f;
            hammer.holdDistance = 0.5f;
            hammer.holdScale = 0.8f;
            hammer.tint = new Color(0.85f, 0.88f, 0.95f);
            SetSprites(hammer, "weapon_hammer");

            // ---- ranged ------------------------------------------------------
            WeaponDefinition bow = Create("Bow");
            bow.displayName = "Bow";
            bow.description = "Fires a bolt. Medium damage, medium rate of fire.";
            bow.kind = WeaponKind.Ranged;
            bow.damage = 22f;
            bow.cooldown = 0.55f;
            bow.knockback = 6f;
            bow.hitShake = 0.18f;
            bow.projectileSpeed = 34f;
            bow.projectileLifetime = 1.6f;
            bow.projectileCount = 1;
            bow.projectileScale = 1f;
            bow.spread = 8f;
            bow.holdDistance = 0.45f;
            bow.holdScale = 0.8f;
            bow.tint = new Color(0.85f, 1f, 0.9f);
            SetSprites(bow, "weapon_bow", "bolt");

            WeaponDefinition wand = Create("Wand");
            wand.displayName = "Wand";
            wand.description = "Rapid fire energy bolts, low damage per hit.";
            wand.kind = WeaponKind.Ranged;
            wand.damage = 13f;
            wand.cooldown = 0.2f;
            wand.knockback = 3.5f;
            wand.hitShake = 0.1f;
            wand.projectileSpeed = 26f;
            wand.projectileLifetime = 1.3f;
            wand.projectileCount = 1;
            wand.projectileScale = 0.85f;
            wand.spread = 4f;
            wand.holdDistance = 0.45f;
            wand.holdScale = 0.8f;
            wand.tint = new Color(0.7f, 0.9f, 1f);
            SetSprites(wand, "weapon_wand", "bolt");

            AssetDatabase.SaveAssets();
            Debug.Log($"[TanLuZhe] Weapon assets ready in {WeaponsDir} ({Names.Length} weapons).");
        }

        public static WeaponDefinition Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponsDir + name + ".asset");
        }

        private static WeaponDefinition Create(string name)
        {
            string path = WeaponsDir + name + ".asset";
            WeaponDefinition asset = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.name = name;
            return asset;
        }

        private static void SetSprites(WeaponDefinition weapon, string worldSpriteName, string projectileName = null)
        {
            weapon.worldSprite = LoadSprite(worldSpriteName);
            weapon.icon = weapon.worldSprite;
            if (!string.IsNullOrEmpty(projectileName)) weapon.projectileSprite = LoadSprite(projectileName);
            EditorUtility.SetDirty(weapon);
        }

        private static Sprite LoadSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
            if (sprite == null)
            {
                Debug.LogWarning($"[TanLuZhe] Missing weapon sprite '{name}'. Run 'TanLuZhe/1. Generate Art Assets' first.");
            }
            return sprite;
        }
    }
}
