using Godot;
using Networking;
using Riptide;
using System.Collections.Generic;

public partial class Player : CharacterBody2D, Damageable, NetworkPointUser {
	public static List<Player> Players = new List<Player>();
	public static List<Player> AlivePlayers = new List<Player>();
	public static Player LocalPlayer;

	[Export] public PackedScene DefaultWeaponScene;
	[Export] public Sprite2D Sprite;

	public List<Trinket> EquippedTrinkets = new List<Trinket>();
	public float Health = 3f;
	public NetworkPoint NetworkPoint { get; set; } = new NetworkPoint();
	public AnimationPlayer AnimationPlayer;
	public StateMachine StateMachine;
	public Vector2 Knockback;

	private NetworkedVariable<Vector2> _networkedPosition = new NetworkedVariable<Vector2>(Vector2.Zero);
	private NetworkedVariable<Vector2> _networkedVelocity = new NetworkedVariable<Vector2>(Vector2.Zero);

	private Weapon _equippedWeapon;

	public override void _Ready() {
		NetworkPoint.Setup(this);

		NetworkPoint.Register(nameof(_networkedPosition), _networkedPosition);
		NetworkPoint.Register(nameof(_networkedVelocity), _networkedVelocity);
		NetworkPoint.Register(nameof(EquipWeaponRpc), EquipWeaponRpc);
		NetworkPoint.Register(nameof(DamageRpc), DamageRpc);
		NetworkPoint.Register(nameof(DieRpc), DieRpc);
		NetworkPoint.Register(nameof(ReviveRpc), ReviveRpc);

		Players.Add(this);
		AlivePlayers.Add(this);

		AnimationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");

		StateMachine = GetNode<StateMachine>("StateMachine");

		EquipDefaultItem();

		if (!NetworkPoint.IsOwner) return;

		LocalPlayer = this;

		GameUI.UpdateHealth(Health);
	}

	public override void _Process(double delta) {
		Knockback = Knockback.Lerp(Vector2.Zero, (float)delta * 12f);

		_networkedPosition.Sync();
		_networkedVelocity.Sync();

		if (NetworkPoint.IsOwner) {
			_networkedPosition.Value = GlobalPosition;
			_networkedVelocity.Value = Velocity;
		} else {
			GlobalPosition = GlobalPosition.Lerp(_networkedPosition.Value, (float)delta * 20.0f);
			Velocity = _networkedVelocity.Value;
		}

		if (!NetworkPoint.IsOwner) return;

		if (StateMachine.CurrentState != "Trinket") Sprite.Scale = GetGlobalMousePosition().X - GlobalPosition.X >= 0 ? Vector2.One : new Vector2(-1, 1);
	}

	public void Heal(float health) {
		if (!NetworkPoint.IsOwner) return;

		Health += health;

		if (Health > 3) Health = 3;

		GameUI.UpdateHealth(Health);
	}

	public bool CanDamage(Projectile projectile) {
		if (projectile.Source is Player) return false;

		if (Health <= 0) return false;

		if (Knockback.Length() >= 1f) return false;

		if (StateMachine.CurrentState == "Dash") return false;

		return true;
	}

	public void Damage(Projectile projectile) {
		if (!NetworkPoint.IsOwner) return;

		Health -= projectile.Damage;

		GameUI.UpdateHealth(Health);

		if (Health <= 0) Die();

		NetworkPoint.BounceRpcToClients(nameof(DamageRpc), message => {
			Vector2 knockback = projectile.GlobalTransform.BasisXform(Vector2.Right) * 200f * projectile.Knockback;

			message.AddFloat(knockback.X);
			message.AddFloat(knockback.Y);
		});
	}

	public void Die() {
		Health = 0;

		GameUI.UpdateHealth(Health);

		AlivePlayers.Remove(this);

		NetworkPoint.BounceRpcToClients(nameof(DieRpc));

		StateMachine.GoToState("Angel");
	}

	public void Revive() {
		NetworkPoint.BounceRpcToClients(nameof(ReviveRpc));
	}

	public void Equip(Item item) {
		NetworkPoint.BounceRpcToClients(nameof(EquipWeaponRpc), message => {
			message.AddString(item.GetPath());
		});
	}

	private void EquipDefaultItem() {
		Weapon weapon = NetworkManager.SpawnNetworkSafe<Weapon>(DefaultWeaponScene, "Weapon");

		AddChild(weapon);

		if (!NetworkPoint.IsOwner) return;

		Equip(weapon);
	}

	public void Cleanup() {
		Players.Remove(this);

		QueueFree();
	}

	public void EnterTrinketRealm() {
		StateMachine.GoToState("Trinket");

		ZIndex += 25;

		GetNode<Node2D>("WeaponHolder").ZIndex -= 25;
	}

	public void LeaveTrinketRealm() {
		StateMachine.GoToState("Normal");

		ZIndex -= 25;

		GetNode<Node2D>("WeaponHolder").ZIndex += 25;
	}

	private void DamageRpc(Message message) {
		Knockback = new Vector2(message.GetFloat(), message.GetFloat());

		AnimationPlayer.Play("hurt");
	}

	private void DieRpc(Message message) {
		if (!NetworkPoint.IsOwner) {
			Health = 0;

			AlivePlayers.Remove(this);

			StateMachine.GoToState("Angel");
		}

		if (AlivePlayers.Count != 0) return;

		if (!NetworkManager.IsHost) return;

		Game.Restart();
	}

	private void EquipWeaponRpc(Message message) {
		string itemPath = message.GetString();

		Item item = GetNode<Item>(itemPath);

		if (item is Weapon) {
			if (_equippedWeapon != null) _equippedWeapon.QueueFree();

			item.GetParent().RemoveChild(item);
			GetNode("WeaponHolder").AddChild(item);
			item.SetMultiplayerAuthority(GetMultiplayerAuthority());

			_equippedWeapon = (Weapon)item;
		}

		if (item is Trinket) {
			item.GetParent().RemoveChild(item);
			GetNode("TrinketsHolder").AddChild(item);
			item.SetMultiplayerAuthority(GetMultiplayerAuthority());

			EquippedTrinkets.Add((Trinket)item);
		}

		item.EquipToPlayer(this);
	}

	private void ReviveRpc(Message message) {
		StateMachine.GoToState("Normal");

		AlivePlayers.Add(this);

		Health = 1;

		if (!NetworkPoint.IsOwner) return;

		GameUI.UpdateHealth(Health);
	}
}
