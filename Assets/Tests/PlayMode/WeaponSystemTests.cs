using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TanLuZhe.Tests
{
    /// <summary>
    /// Play mode tests for the weapon / attack system: F pick-up, backpack, B inventory UI,
    /// left mouse = main hand and right mouse = off hand.
    /// </summary>
    public sealed class WeaponSystemTests
    {
        private GameObject _root;
        private ScriptedInputSource _input;
        private readonly List<Object> _created = new List<Object>();
        private static PhysicsMaterial2D _frictionless;

        private sealed class ScriptedInputSource : IInputSource
        {
            // Edge flags self-clear on the first read so a press is consumed exactly once, no
            // matter how many Update calls the test host squeezes into one frame.
            private bool _jumpDown;
            private bool _grappleDown;
            private bool _interactDown;
            private bool _inventoryDown;
            private bool _attackMainDown;
            private bool _attackOffDown;

            public float Horizontal { get; set; }
            public bool JumpDown { get => Consume(ref _jumpDown); set => _jumpDown = value; }
            public bool JumpHeld { get; set; }
            public bool GrappleDown { get => Consume(ref _grappleDown); set => _grappleDown = value; }
            public bool ReleaseHeld { get; set; }
            public bool DownHeld { get; set; }
            public bool InteractDown { get => Consume(ref _interactDown); set => _interactDown = value; }
            public bool InventoryDown { get => Consume(ref _inventoryDown); set => _inventoryDown = value; }
            public bool AttackMainDown { get => Consume(ref _attackMainDown); set => _attackMainDown = value; }
            public bool AttackMainHeld { get; set; }
            public bool AttackOffDown { get => Consume(ref _attackOffDown); set => _attackOffDown = value; }
            public bool AttackOffHeld { get; set; }
            public Vector2 PointerScreen { get; set; }

            private static bool Consume(ref bool flag)
            {
                if (!flag) return false;
                flag = false;
                return true;
            }
        }

        private struct Rig
        {
            public GameObject Root;
            public PlayerController2D Controller;
            public PlayerWeapons Weapons;
            public Rigidbody2D Body;
        }

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("WeaponTestRoot");
            _input = new ScriptedInputSource();
            GameLayers.ApplyCollisionRules();
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
            _root = null;

            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null) Object.Destroy(_created[i]);
            }
            _created.Clear();
        }

        private static PhysicsMaterial2D Frictionless()
        {
            if (_frictionless == null)
                _frictionless = new PhysicsMaterial2D("WeaponTestFrictionless") { friction = 0f, bounciness = 0f };
            return _frictionless;
        }

        private static IEnumerator StepFixed(int steps)
        {
            for (int i = 0; i < steps; i++) yield return new WaitForFixedUpdate();
        }

        /// <summary>Writes a private [SerializeField] field before the component's Awake runs.</summary>
        private static void SetField(Object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Serialized field '{fieldName}' not found on {target.GetType().Name}");
            field.SetValue(target, value);
        }

        private WeaponDefinition MakeWeapon(string name, WeaponKind kind, float damage, float cooldown)
        {
            WeaponDefinition weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            weapon.name = name;
            weapon.displayName = name;
            weapon.kind = kind;
            weapon.damage = damage;
            weapon.cooldown = cooldown;
            weapon.knockback = 0f;                 // keep targets put so tests measure damage only
            weapon.meleeSize = new Vector2(2f, 1.6f);
            weapon.meleeReach = 0.9f;
            weapon.projectileSpeed = 30f;
            weapon.projectileLifetime = 1.5f;
            weapon.projectileCount = 1;
            weapon.projectileScale = 1f;
            weapon.holdDistance = 0.5f;
            weapon.holdScale = 1f;
            _created.Add(weapon);
            return weapon;
        }

        private Rig MakePlayer(Vector2 position)
        {
            Rig rig = new Rig();

            GameObject go = new GameObject("Player");
            go.SetActive(false);                       // configure before Awake runs
            go.transform.SetParent(_root.transform, false);
            go.layer = GameLayers.Player;
            go.transform.position = position;
            rig.Root = go;

            rig.Body = go.AddComponent<Rigidbody2D>();
            rig.Body.gravityScale = 0f;
            rig.Body.mass = 1f;
            rig.Body.freezeRotation = true;

            CapsuleCollider2D capsule = go.AddComponent<CapsuleCollider2D>();
            capsule.direction = CapsuleDirection2D.Vertical;
            capsule.size = new Vector2(0.72f, 1.5f);
            capsule.sharedMaterial = Frictionless();

            GameObject hand = new GameObject("HandPivot");
            hand.transform.SetParent(go.transform, false);
            hand.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            rig.Controller = go.AddComponent<PlayerController2D>();
            rig.Weapons = go.AddComponent<PlayerWeapons>();
            SetField(rig.Weapons, "_handOrigin", hand.transform);
            SetField(rig.Weapons, "_hitMask", (LayerMask)(1 << GameLayers.Enemy));

            go.SetActive(true);

            rig.Controller.SetInputSource(_input);
            rig.Weapons.SetInputSource(_input);

            // The weapon tests are about attacks and inventory, not locomotion: freezing the
            // character controller keeps the player exactly where it was placed (no gravity
            // drift), so a melee box aimed at an enemy still reaches it frames later.
            rig.Controller.enabled = false;
            return rig;
        }

        private EnemyController2D MakeEnemy(Vector2 position, float health)
        {
            GameObject go = new GameObject("Enemy");
            go.SetActive(false);
            go.transform.SetParent(_root.transform, false);
            go.layer = GameLayers.Enemy;
            go.transform.position = position;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.gravityScale = 0f;
            rb.mass = 2f;
            rb.freezeRotation = true;

            BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.8f);
            collider.sharedMaterial = Frictionless();

            EnemyController2D enemy = go.AddComponent<EnemyController2D>();
            enemy.enabled = false;   // no AI in the tests
            SetField(enemy, "_maxHealth", health);

            go.SetActive(true);
            return enemy;
        }

        [UnityTest]
        public IEnumerator MeleeAttack_SlashFxBulgesAlongTheSwing()
        {
            GameObject services = new GameObject("Services");
            services.SetActive(false);
            services.transform.SetParent(_root.transform, false);
            FxManager fx = services.AddComponent<FxManager>();
            SetField(fx, "_poolSize", 8);
            services.SetActive(true);
            yield return null;

            // The slash art is a '(' opening toward +X, so the offset must turn it into a ')'
            // that bulges in the attack direction with the hollow facing the attacker.
            Transform right = FxManager.Slash(Vector2.zero, Vector2.right);
            Assert.IsNotNull(right, "The slash FX should spawn a particle.");
            Assert.That(Mathf.DeltaAngle(right.eulerAngles.z, 180f), Is.EqualTo(0f).Within(0.1f),
                "A rightward swing must rotate the arc 180 deg so it opens toward the player.");

            Transform up = FxManager.Slash(Vector2.zero, Vector2.up);
            Assert.IsNotNull(up);
            Assert.That(Mathf.DeltaAngle(up.eulerAngles.z, 270f), Is.EqualTo(0f).Within(0.1f),
                "The offset must follow the attack direction, not be a fixed world angle.");

            Transform left = FxManager.Slash(Vector2.zero, Vector2.left);
            Assert.IsNotNull(left);
            Assert.That(Mathf.DeltaAngle(left.eulerAngles.z, 0f), Is.EqualTo(0f).Within(0.1f));
        }

        // ================================================================ pick up
        [UnityTest]
        public IEnumerator PickUp_F_AddsWeaponToBackpack()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            WeaponDefinition spear = MakeWeapon("Spear", WeaponKind.Melee, 20f, 0.3f);

            GameObject pickupGO = new GameObject("Spear Pickup");
            pickupGO.transform.SetParent(_root.transform, false);
            pickupGO.layer = GameLayers.Collectible;
            pickupGO.transform.position = new Vector2(0f, 2f);
            CircleCollider2D collider = pickupGO.AddComponent<CircleCollider2D>();
            collider.radius = 1.4f;
            collider.isTrigger = true;
            WeaponPickup2D pickup = pickupGO.AddComponent<WeaponPickup2D>();
            SetField(pickup, "_weapon", spear);

            yield return StepFixed(4);

            Assert.AreEqual(pickup, player.Weapons.NearbyPickup, "Standing in the pickup should register it.");

            _input.InteractDown = true;
            yield return null;
            _input.InteractDown = false;

            Assert.AreEqual(1, player.Weapons.Backpack.Count, "F must move the weapon into the backpack.");
            Assert.AreEqual(spear, player.Weapons.Backpack[0]);
            Assert.IsTrue(pickupGO == null, "The world pickup must be consumed.");
        }

        [UnityTest]
        public IEnumerator PickUp_BackpackRespectsCapacity()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(player.Weapons.TryAddWeapon(MakeWeapon($"W{i}", WeaponKind.Melee, 10f, 0.2f)));

            WeaponDefinition extra = MakeWeapon("Extra", WeaponKind.Melee, 10f, 0.2f);
            Assert.IsFalse(player.Weapons.TryAddWeapon(extra), "A full backpack must refuse the weapon.");
            Assert.AreEqual(8, player.Weapons.Backpack.Count);
            yield return null;
        }

        // ================================================================ equip
        [UnityTest]
        public IEnumerator Equip_SwapsBetweenHandsAndBackpack()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.4f);
            WeaponDefinition bow = MakeWeapon("Bow", WeaponKind.Ranged, 22f, 0.5f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.TryAddWeapon(bow);

            Assert.IsTrue(player.Weapons.Equip(sword, false));
            Assert.AreEqual(sword, player.Weapons.MainHand);
            CollectionAssert.DoesNotContain(player.Weapons.Backpack, sword);

            // equipping another weapon into the same hand sends the old one back
            Assert.IsTrue(player.Weapons.Equip(bow, false));
            Assert.AreEqual(bow, player.Weapons.MainHand);
            CollectionAssert.Contains(player.Weapons.Backpack, sword);

            Assert.IsTrue(player.Weapons.Equip(sword, true));
            Assert.AreEqual(sword, player.Weapons.OffHand);

            Assert.IsTrue(player.Weapons.Unequip(true));
            Assert.IsNull(player.Weapons.OffHand);
            CollectionAssert.Contains(player.Weapons.Backpack, sword);
            yield return null;
        }

        // ================================================================ attacks
        [UnityTest]
        public IEnumerator MeleeAttack_DamagesEnemyInFront()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(1.6f, 2f), 100f);
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.4f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.Equip(sword, false);

            yield return StepFixed(2);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;

            Assert.That(enemy.Health, Is.EqualTo(72f).Within(0.01f),
                "The main hand melee attack must hit the enemy in front of the player.");
        }

        [UnityTest]
        public IEnumerator MeleeAttack_RespectsCooldown()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(1.6f, 2f), 200f);
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.5f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.Equip(sword, false);

            yield return StepFixed(2);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;
            float afterFirst = enemy.Health;
            Assert.That(afterFirst, Is.LessThan(200f), "The first attack must land.");

            // immediately attacking again must be swallowed by the cooldown
            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;
            Assert.AreEqual(afterFirst, enemy.Health, "A second attack during the cooldown must not land.");

            yield return StepFixed(32);
            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;
            Assert.That(enemy.Health, Is.LessThan(afterFirst), "Once the cooldown is over the attack must land again.");
        }

        [UnityTest]
        public IEnumerator RangedAttack_ProjectileDamagesEnemy()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(4f, 2f), 100f);
            WeaponDefinition bow = MakeWeapon("Bow", WeaponKind.Ranged, 22f, 0.5f);
            player.Weapons.TryAddWeapon(bow);
            player.Weapons.Equip(bow, true);

            yield return StepFixed(2);

            _input.AttackOffDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackOffDown = false;

            yield return StepFixed(12);

            Assert.That(enemy.Health, Is.EqualTo(78f).Within(0.01f),
                "The off hand ranged attack must fire a projectile that damages the enemy.");
        }

        [UnityTest]
        public IEnumerator LeftMouseUsesMainHand_RightMouseUsesOffHand()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(1.6f, 2f), 200f);

            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.3f);
            WeaponDefinition dagger = MakeWeapon("Dagger", WeaponKind.Melee, 7f, 0.3f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.TryAddWeapon(dagger);
            player.Weapons.Equip(sword, false);
            player.Weapons.Equip(dagger, true);

            yield return StepFixed(2);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;
            Assert.That(enemy.Health, Is.EqualTo(172f).Within(0.01f), "Left mouse must use the main hand weapon (28).");

            yield return StepFixed(25);

            _input.AttackOffDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackOffDown = false;
            Assert.That(enemy.Health, Is.EqualTo(165f).Within(0.01f), "Right mouse must use the off hand weapon (7).");
        }

        [UnityTest]
        public IEnumerator Attack_BlockedWhileRopeIsAttached()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(1.6f, 2f), 100f);
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.3f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.Equip(sword, false);

            yield return StepFixed(2);

            player.Controller.SetBeingPulled(true);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;

            Assert.That(enemy.Health, Is.EqualTo(100f).Within(0.01f),
                "Attacks must be inert while the grapple rope owns the character.");

            player.Controller.SetBeingPulled(false);
            yield return StepFixed(2);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;

            Assert.That(enemy.Health, Is.LessThan(100f), "Control must come back after the rope is gone.");
        }

        [UnityTest]
        public IEnumerator Attack_BlockedWhileBackpackIsOpen()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            EnemyController2D enemy = MakeEnemy(new Vector2(1.6f, 2f), 100f);
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.3f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.Equip(sword, false);

            yield return StepFixed(2);

            player.Weapons.SetInventoryOpen(true);

            _input.AttackMainDown = true;
            yield return new WaitForFixedUpdate();
            _input.AttackMainDown = false;

            Assert.That(enemy.Health, Is.EqualTo(100f).Within(0.01f),
                "Left clicks belong to the backpack UI while it is open.");
        }

        // ================================================================ inventory
        [UnityTest]
        public IEnumerator Inventory_BKeyTogglesThePanel()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            yield return StepFixed(2);

            Assert.IsFalse(player.Weapons.IsInventoryOpen);

            _input.InventoryDown = true;
            yield return null;
            _input.InventoryDown = false;
            Assert.IsTrue(player.Weapons.IsInventoryOpen, "B must open the backpack.");

            _input.InventoryDown = true;
            yield return null;
            _input.InventoryDown = false;
            Assert.IsFalse(player.Weapons.IsInventoryOpen, "B must close it again.");
        }

        [UnityTest]
        public IEnumerator InventoryUI_ClickEquipsIntoTheSelectedHand()
        {
            Rig player = MakePlayer(new Vector2(0f, 2f));
            WeaponDefinition sword = MakeWeapon("Sword", WeaponKind.Melee, 28f, 0.3f);
            WeaponDefinition bow = MakeWeapon("Bow", WeaponKind.Ranged, 22f, 0.5f);
            player.Weapons.TryAddWeapon(sword);
            player.Weapons.TryAddWeapon(bow);

            GameObject canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(_root.transform, false);
            canvasGO.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            WeaponInventoryUI ui = canvasGO.AddComponent<WeaponInventoryUI>();
            SetField(ui, "_weapons", player.Weapons);

            yield return null;
            yield return null;

            Assert.AreEqual(2, ui.HandSlotCount, "The panel must build both hand slots.");
            Assert.AreEqual(8, ui.BackpackSlotCount, "The panel must build the backpack grid.");

            player.Weapons.SetInventoryOpen(true);
            yield return null;
            Assert.IsTrue(ui.IsOpen, "Opening the backpack must show the panel.");

            // click backpack slot 0 -> equips into the main hand (default selection)
            Assert.IsTrue(ui.ClickBackpackSlot(0));
            Assert.AreEqual(sword, player.Weapons.MainHand, "Clicking a backpack weapon must equip it.");

            // select the off hand, then click the remaining backpack weapon
            Assert.IsTrue(ui.ClickHandSlot(1));
            Assert.AreEqual(1, ui.SelectedHand);
            Assert.IsTrue(ui.ClickBackpackSlot(0));
            Assert.AreEqual(bow, player.Weapons.OffHand, "The selected hand must receive the weapon.");
        }
    }
}
