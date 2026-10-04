using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "func_guntarget" )]
public class GunTarget : BaseMonster
{
	private const int SpawnFlagStartOn = 0x0001;

	[Save( FieldType.Boolean )]
	private bool _on;

	public override int BloodColor => DontBleed;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public override int Classify() => ClassMachine;

	public override Vector3 BodyTarget( Vector3 source ) => Origin;

	public override void Spawn()
	{
		Solid = Solid.Bsp;
		MoveType = MoveType.Push;

		SetOrigin( Origin );
		SetModel( ModelName );

		if ( Speed == 0f )
			Speed = 100f;

		DamageMode = DamageNo;
		EntityFlags |= EntityFlags.Monster;

		_on = false;
		MaxHealth = Health;

		if ( (SpawnFlags & SpawnFlagStartOn) != 0 )
		{
			SetThink( Start );
			NextThink = (float)(LTime + 0.3);
		}
	}

	public override void Activate()
	{
		if ( GetNextTarget() is not { } target )
			return;

		TargetEntity = target;
		SetOrigin( target.Origin - ((Mins + Maxs) * 0.5f) );
	}

	public void Start() => Use( this, this, UseType.On, 0f );

	public void Next()
	{
		SetThink( null );

		TargetEntity = GetNextTarget();

		if ( TargetEntity is not { } target )
		{
			Stop();
			return;
		}

		SetMoveDone( WaitAtTarget );
		LinearMove( target.Origin - ((Mins + Maxs) * 0.5f), Speed );
	}

	public void WaitAtTarget()
	{
		if ( TargetEntity is not { } target )
		{
			Stop();
			return;
		}

		if ( !string.IsNullOrEmpty( target.Message ) )
		{
			FireTargets( target.Message, this, this, UseType.Toggle, 0f );

			if ( (target.SpawnFlags & PathCorner.SpawnFlagFireOnce) != 0 )
				target.Message = null;
		}

		Wait = target.GetDelay();

		Target = target.Target;
		SetThink( Next );

		if ( Wait != 0f )
			NextThink = LTime + Wait;
		else
			Next();
	}

	public override void Stop()
	{
		Velocity = Vector3.Zero;
		NextThink = 0f;
		DamageMode = DamageNo;
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		if ( Health > 0f )
		{
			Health -= damage;

			if ( Health <= 0f )
			{
				Health = 0f;
				Stop();

				if ( !string.IsNullOrEmpty( Message ) )
					FireTargets( Message, this, this, UseType.Toggle, 0f );
			}
		}

		return false;
	}

	public override void Use( BaseEntity activator, BaseEntity caller, UseType useType, float value )
	{
		if ( !ShouldToggle( useType, _on ) )
			return;

		if ( _on )
		{
			Stop();
			return;
		}

		DamageMode = DamageAim;
		TargetEntity = GetNextTarget();

		if ( TargetEntity is null )
			return;

		Health = MaxHealth;
		Next();
	}
}
