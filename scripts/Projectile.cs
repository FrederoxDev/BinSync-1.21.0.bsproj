using Godot;
using System;

public partial class Projectile : Node2D {
	[Export] public float Damage = 1f;
	[Export] public float Speed = 200f;
	[Export] public float Resistance = 0f;
	[Export] public float Lifetime = 5f;
	[Export] public float Invincibilitytime = 0.2f;
	[Export] public float Knockback = 1f;
	[Export] public bool InheritBelocity = true;
	[Export] public bool Pierce = false;

	public Action Destroyed;

	public Node2D Source;
	public Vector2 InheritedVelocity;

	private Area2D _damageArea;
	private float _lifetimeTimer;
	private float _invincibilityTimer;

	public override void _Ready() {
		_damageArea = GetNode<Area2D>("DamageArea");

		_lifetimeTimer = Lifetime;
		_invincibilityTimer = Invincibilitytime;
	}

	public override void _Process(double delta) {
		_lifetimeTimer -= (float)delta;
		_invincibilityTimer -= (float)delta;

		if (_lifetimeTimer > 0) return;

		Destroyed?.Invoke();

		QueueFree();
	}

	public override void _PhysicsProcess(double delta) {
		if (InheritBelocity && IsInstanceValid(Source) && Source is CharacterBody2D) InheritedVelocity = (Source as CharacterBody2D).Velocity;

		GlobalPosition += GlobalTransform.BasisXform(Vector2.Right) * Speed * (float)delta + InheritedVelocity * (float)delta;

		Speed = Mathf.Lerp(Speed, 0f, Resistance * (float)delta);

		foreach (Node2D body in _damageArea.GetOverlappingBodies()) {
			if ((body is TileMap || body is Barrier) && _invincibilityTimer <= 0) {
				Destroyed?.Invoke();

				QueueFree();

				return;
			}

			if (!(body is Damageable)) continue;

			Damageable damageable = body as Damageable;

			if (!damageable.CanDamage(this)) continue;

			damageable.Damage(this);

			Audio.Play("projectile_hit");

			if (!Pierce) {
				Destroyed?.Invoke();

				QueueFree();

				break;
			}
		}
	}
}
