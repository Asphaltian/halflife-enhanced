using GoldSrc;

namespace HalfLife;

public class ActAnimating : BaseAnimating
{
	[Save( FieldType.Integer )]
	private Activity _activity;

	public override ObjectCaps ObjectCaps => base.ObjectCaps & ~ObjectCaps.AcrossTransition;

	public Activity GetActivity() => _activity;

	public void SetActivity( Activity activity )
	{
		var sequence = LookupActivity( (int)activity );

		if ( sequence == Animation.ActivityNotAvailable )
			return;

		Sequence = sequence;
		_activity = activity;
		Frame = 0f;
		ResetSequenceInfo();
	}
}

[LinkEntityToClass( "xen_plantlight" )]
public class XenPLight : ActAnimating
{
	public const string GlowSprite = "sprites/flare3.spr";

	private const float HideTime = 5f;

	[Save( FieldType.ClassPtr )]
	private Sprite _glow;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/light.mdl" );
		MoveType = MoveType.None;
		Solid = Solid.Trigger;

		SetSize( new Vector3( -80f, -80f, 0f ), new Vector3( 80f, 80f, 32f ) );
		SetActivity( Activity.Idle );
		NextThink = (float)(Server.Globals.Time + 0.1);
		Frame = Server.RandomFloat( 0f, 255f );

		_glow = Sprite.SpriteCreate( Server, GlowSprite, Origin + new Vector3( 0f, 0f, (Mins.z + Maxs.z) * 0.5f ), false );
		_glow.SetTransparency( RenderMode.Glow, (int)RenderColor.x, (int)RenderColor.y, (int)RenderColor.z, (int)RenderAmt, RenderFx );
		_glow.SetAttachment( this, 1 );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/light.mdl" );
		Server.PrecacheModel( GlowSprite );
	}

	public override void Think()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);

		switch ( GetActivity() )
		{
			case Activity.Crouch:
				if ( SequenceFinished )
				{
					SetActivity( Activity.CrouchIdle );
					LightOff();
				}

				break;

			case Activity.CrouchIdle:
				if ( Server.Globals.Time > DamageTime )
				{
					SetActivity( Activity.Stand );
					LightOn();
				}

				break;

			case Activity.Stand:
				if ( SequenceFinished )
					SetActivity( Activity.Idle );

				break;
		}
	}

	public override void Touch( BaseEntity other )
	{
		if ( !other.IsPlayer )
			return;

		DamageTime = Server.Globals.Time + HideTime;

		if ( GetActivity() is Activity.Idle or Activity.Stand )
			SetActivity( Activity.Crouch );
	}

	public void LightOn()
	{
		UseTargets( this, UseType.On, 0f );

		if ( _glow is not null )
			_glow.Effects &= ~Effects.NoDraw;
	}

	public void LightOff()
	{
		UseTargets( this, UseType.Off, 0f );

		if ( _glow is not null )
			_glow.Effects |= Effects.NoDraw;
	}
}

[LinkEntityToClass( "xen_hair" )]
public class XenHair : ActAnimating
{
	private const int SpawnFlagSync = 0x0001;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/hair.mdl" );
		SetSize( new Vector3( -4f, -4f, 0f ), new Vector3( 4f, 4f, 32f ) );
		Sequence = 0;

		if ( (SpawnFlags & SpawnFlagSync) == 0 )
		{
			Frame = Server.RandomFloat( 0f, 255f );
			FrameRate = Server.RandomFloat( 0.7f, 1.4f );
		}

		ResetSequenceInfo();

		Solid = Solid.Not;
		MoveType = MoveType.None;
		NextThink = Server.Globals.Time + Server.RandomFloat( 0.1f, 0.4f );
	}

	public override void Think()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.5);
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/hair.mdl" );
	}
}

[LinkEntityToClass( "xen_ttrigger" )]
public class XenTreeTrigger : BaseEntity
{
	public static XenTreeTrigger TriggerCreate( BaseEntity owner, Vector3 position )
	{
		var trigger = owner.Server.CreateEntity<XenTreeTrigger>();

		trigger.Origin = position;
		trigger.ClassName = "xen_ttrigger";
		trigger.Solid = Solid.Trigger;
		trigger.MoveType = MoveType.None;
		trigger.Owner = owner;

		return trigger;
	}

	public override void Touch( BaseEntity other )
	{
		if ( Owner is BaseEntity owner )
			owner.Touch( other );
	}
}

[LinkEntityToClass( "xen_tree" )]
public class XenTree : ActAnimating
{
	private const int EventAttack = 1;

	private static readonly string[] AttackHitSounds =
	[
		"zombie/claw_strike1.wav",
		"zombie/claw_strike2.wav",
		"zombie/claw_strike3.wav"
	];

	private static readonly string[] AttackMissSounds =
	[
		"zombie/claw_miss1.wav",
		"zombie/claw_miss2.wav"
	];

	[Save( FieldType.ClassPtr )]
	private XenTreeTrigger _trigger;

	public override int Classify() => ClassBarnacle;

	public override void Spawn()
	{
		Precache();

		SetModel( "models/tree.mdl" );
		MoveType = MoveType.None;
		Solid = Solid.BBox;

		DamageMode = DamageYes;

		SetSize( new Vector3( -30f, -30f, 0f ), new Vector3( 30f, 30f, 188f ) );
		SetActivity( Activity.Idle );
		NextThink = (float)(Server.Globals.Time + 0.1);
		Frame = Server.RandomFloat( 0f, 255f );
		FrameRate = Server.RandomFloat( 0.7f, 1.4f );

		MathLib.AngleVectors( Angles, out var forward, out _, out _ );

		_trigger = XenTreeTrigger.TriggerCreate( this, Origin + (forward * 64f) );
		Server.SetSize( _trigger, new Vector3( -24f, -24f, 0f ), new Vector3( 24f, 24f, 128f ) );
	}

	public override void Precache()
	{
		Server.PrecacheModel( "models/tree.mdl" );
		Server.PrecacheModel( XenPLight.GlowSprite );
		PrecacheSounds( AttackHitSounds );
		PrecacheSounds( AttackMissSounds );
	}

	public override void Touch( BaseEntity other )
	{
		if ( !other.IsPlayer && other.ClassName == "monster_bigmomma" )
			return;

		Attack();
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType )
	{
		Attack();

		return false;
	}

	public void Attack()
	{
		if ( GetActivity() != Activity.Idle )
			return;

		SetActivity( Activity.MeleeAttack1 );
		FrameRate = Server.RandomFloat( 1f, 1.4f );
		Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackMissSounds ), 1f, Server.AttenuationNormal, 0, Server.RandomLong( 95, 105 ) );
	}

	public override void HandleAnimEvent( MonsterEvent monsterEvent )
	{
		if ( monsterEvent.Event != EventAttack )
		{
			base.HandleAnimEvent( monsterEvent );
			return;
		}

		var list = new BaseEntity[8];
		var sound = false;
		var count = Util.EntitiesInBox( list, _trigger.AbsMin, _trigger.AbsMax, EntityFlags.Monster | EntityFlags.Client );

		MathLib.AngleVectors( Angles, out var forward, out _, out _ );

		for ( var i = 0; i < count; i++ )
		{
			if ( list[i] == this || list[i].Owner == this )
				continue;

			sound = true;
			list[i].TakeDamage( this, this, 25f, DamageCrush | DamageSlash );
			list[i].PunchAngle = list[i].PunchAngle with { x = 15f };
			list[i].Velocity += forward * 100f;
		}

		if ( sound )
			Sound.EmitDyn( this, SoundChannel.Weapon, RandomSound( AttackHitSounds ), 1f, Server.AttenuationNormal, 0, Server.RandomLong( 95, 105 ) );
	}

	public override void Think()
	{
		var interval = StudioFrameAdvance();

		NextThink = (float)(Server.Globals.Time + 0.1);
		DispatchAnimEvents( interval );

		if ( GetActivity() == Activity.MeleeAttack1 && SequenceFinished )
		{
			SetActivity( Activity.Idle );
			FrameRate = Server.RandomFloat( 0.6f, 1.4f );
		}
	}
}

public class XenSpore : ActAnimating
{
	private static readonly string[] ModelNames =
	[
		"models/fungus(small).mdl",
		"models/fungus.mdl",
		"models/fungus(large).mdl"
	];

	public override void Spawn()
	{
		Precache();

		SetModel( ModelNames[Skin] );
		MoveType = MoveType.None;
		Solid = Solid.BBox;
		DamageMode = DamageYes;

		Sequence = 0;
		Frame = Server.RandomFloat( 0f, 255f );
		FrameRate = Server.RandomFloat( 0.7f, 1.4f );
		ResetSequenceInfo();
		NextThink = Server.Globals.Time + Server.RandomFloat( 0.1f, 0.4f );
	}

	public override void Precache()
	{
		Server.PrecacheModel( ModelNames[Skin] );
	}

	public override void Touch( BaseEntity other )
	{
	}

	public override void Think()
	{
		StudioFrameAdvance();
		NextThink = (float)(Server.Globals.Time + 0.1);
	}

	public override bool TakeDamage( BaseEntity inflictor, BaseEntity attacker, float damage, int damageType ) => false;
}

[LinkEntityToClass( "xen_spore_small" )]
public class XenSporeSmall : XenSpore
{
	public override void Spawn()
	{
		Skin = 0;
		base.Spawn();
		SetSize( new Vector3( -16f, -16f, 0f ), new Vector3( 16f, 16f, 64f ) );
	}
}

[LinkEntityToClass( "xen_spore_medium" )]
public class XenSporeMed : XenSpore
{
	public override void Spawn()
	{
		Skin = 1;
		base.Spawn();
		SetSize( new Vector3( -40f, -40f, 0f ), new Vector3( 40f, 40f, 120f ) );
	}
}

[LinkEntityToClass( "xen_spore_large" )]
public class XenSporeLarge : XenSpore
{
	private static readonly Vector3[] HullSizes =
	[
		new( 90f, -25f, 0f ),
		new( 25f, 75f, 0f ),
		new( -15f, -100f, 0f ),
		new( -90f, -35f, 0f ),
		new( -90f, 60f, 0f )
	];

	public override void Spawn()
	{
		Skin = 2;
		base.Spawn();
		SetSize( new Vector3( -48f, -48f, 110f ), new Vector3( 48f, 48f, 240f ) );

		MathLib.AngleVectors( Angles, out var forward, out var right, out _ );

		foreach ( var hull in HullSizes )
			XenHull.CreateHull( this, new Vector3( -12f, -12f, 0f ), new Vector3( 12f, 12f, 120f ), (hull.x * forward) + (hull.y * right) );
	}
}

[LinkEntityToClass( "xen_hull" )]
public class XenHull : PointEntity
{
	public override int Classify() => ClassBarnacle;

	public static XenHull CreateHull( BaseEntity source, Vector3 mins, Vector3 maxs, Vector3 offset )
	{
		var server = source.Server;
		var hull = server.CreateEntity<XenHull>();

		server.SetOrigin( hull, source.Origin + offset );
		server.SetModel( hull, source.ModelName );
		hull.Solid = Solid.BBox;
		hull.ClassName = "xen_hull";
		hull.MoveType = MoveType.None;
		hull.Owner = source;
		server.SetSize( hull, mins, maxs );
		hull.RenderAmt = 0f;
		hull.RenderMode = RenderMode.TransTexture;

		return hull;
	}
}
