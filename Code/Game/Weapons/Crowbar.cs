using GoldSrc;

namespace HalfLife;

[LinkEntityToClass( "weapon_crowbar" )]
public class Crowbar : BasePlayerWeapon
{
	public const int WeaponId = 1;
	public const int WeaponWeight = 0;

	private const int BodyHitVolume = 128;
	private const int WallHitVolume = 512;

	private const int AnimIdle = 0;
	private const int AnimDraw = 1;
	private const int AnimHolster = 2;
	private const int AnimAttack1Hit = 3;
	private const int AnimAttack1Miss = 4;
	private const int AnimAttack2Miss = 5;
	private const int AnimAttack2Hit = 6;
	private const int AnimAttack3Miss = 7;
	private const int AnimAttack3Hit = 8;

	private static readonly Vector3 DuckHullMin = new( -16f, -16f, -18f );
	private static readonly Vector3 DuckHullMax = new( 16f, 16f, 18f );

	private int _swing;
	private Trace _hitTrace;
	private int _crowbarEvent;

	public override int ItemSlot => 1;

	public override bool UseDecrement => true;

	public override void Spawn()
	{
		Precache();

		Id = WeaponId;
		SetModel( "models/w_crowbar.mdl" );
		Clip = -1;

		FallInit();
	}

	public override void Precache()
	{
		PrecacheModel( "models/v_crowbar.mdl" );
		PrecacheModel( "models/w_crowbar.mdl" );
		PrecacheModel( "models/p_crowbar.mdl" );
		PrecacheSound( "weapons/cbar_hit1.wav" );
		PrecacheSound( "weapons/cbar_hit2.wav" );
		PrecacheSound( "weapons/cbar_hitbod1.wav" );
		PrecacheSound( "weapons/cbar_hitbod2.wav" );
		PrecacheSound( "weapons/cbar_hitbod3.wav" );
		PrecacheSound( "weapons/cbar_miss1.wav" );

		_crowbarEvent = PrecacheEvent( 1, "events/crowbar.sc" );
	}

	public override bool GetItemInfo( out ItemInfo info )
	{
		info = new ItemInfo
		{
			Name = ClassName,
			Ammo1 = null,
			MaxAmmo1 = -1,
			Ammo2 = null,
			MaxAmmo2 = -1,
			MaxClip = NoClip,
			Slot = 0,
			Position = 0,
			Id = WeaponId,
			Weight = WeaponWeight
		};

		return true;
	}

	public override bool Deploy()
	{
		return DefaultDeploy( "models/v_crowbar.mdl", "models/p_crowbar.mdl", AnimDraw, "crowbar" );
	}

	public override void Holster( int skipLocal = 0 )
	{
		Player.NextAttack = (float)(WeaponTimeBase + 0.5);
		SendWeaponAnim( AnimHolster );
	}

	public override void PrimaryAttack()
	{
		if ( !Swing( true ) )
		{
			SetThink( SwingAgain );
			NextThink = (float)(Globals.Time + 0.1);
		}
	}

	private void Smack()
	{
		Combat.DecalGunshot( _hitTrace, Bullet.PlayerCrowbar );
	}

	private void SwingAgain()
	{
		Swing( false );
	}

	private static void FindHullIntersection( Vector3 source, ref Trace trace, Vector3 mins, Vector3 maxs, Edict entity )
	{
		Vector3[] minMaxs = [mins, maxs];

		var distance = 1e6f;
		var hullEnd = source + ((trace.EndPos - source) * 2f);
		var tempTrace = Util.TraceLine( source, hullEnd, DontIgnoreMonsters, entity );

		if ( tempTrace.Fraction < 1f )
		{
			trace = tempTrace;
			return;
		}

		for ( var i = 0; i < 2; i++ )
		{
			for ( var j = 0; j < 2; j++ )
			{
				for ( var k = 0; k < 2; k++ )
				{
					var end = new Vector3( hullEnd.x + minMaxs[i].x, hullEnd.y + minMaxs[j].y, hullEnd.z + minMaxs[k].z );

					tempTrace = Util.TraceLine( source, end, DontIgnoreMonsters, entity );

					if ( tempTrace.Fraction >= 1f )
						continue;

					var thisDistance = MathLib.Length( tempTrace.EndPos - source );

					if ( thisDistance < distance )
					{
						trace = tempTrace;
						distance = thisDistance;
					}
				}
			}
		}
	}

	private bool Swing( bool first )
	{
		MakeVectors( Player.ViewAngle );

		var source = Player.GetGunPosition();
		var end = source + (Globals.Forward * 32f);
		var trace = TraceLine( source, end, DontIgnoreMonsters, Player );

		if ( !ClientSide && trace.Fraction >= 1f )
		{
			trace = Util.TraceHull( source, end, DontIgnoreMonsters, HeadHull, Player );

			if ( trace.Fraction < 1f )
			{
				if ( (trace.Ent ?? World) is not BaseEntity hit || hit.IsBspModel )
					FindHullIntersection( source, ref trace, DuckHullMin, DuckHullMax, Player );

				end = trace.EndPos;
			}
		}

		if ( first )
			PlaybackEvent( EventFlags.NotHost, Player, _crowbarEvent, 0f, Vector3.Zero, Vector3.Zero, 0f, 0f, 0, 0, 0, 0 );

		if ( trace.Fraction >= 1f )
		{
			if ( first )
			{
				NextPrimaryAttack = GetNextAttackDelay( 0.5f );
				Player.SetAnimation( PlayerAnim.Attack1 );
			}

			return false;
		}

		switch ( (_swing++ % 2) + 1 )
		{
			case 0:
				SendWeaponAnim( AnimAttack1Hit );
				break;
			case 1:
				SendWeaponAnim( AnimAttack2Hit );
				break;
			case 2:
				SendWeaponAnim( AnimAttack3Hit );
				break;
		}

		Player.SetAnimation( PlayerAnim.Attack1 );

		if ( !ClientSide )
		{
			var entity = (trace.Ent ?? World) as BaseEntity;

			Combat.RagdollImpact( source, trace.EndPos );
			Combat.ClearMultiDamage();

			if ( NextPrimaryAttack + 1f <= WeaponTimeBase || HalfLifeDll.GameRules.IsMultiplayer() )
			{
				ScenePhysicsDamage.MeleeImpact( trace, HalfLifeDll.SkillData.PlrDmgCrowbar, Globals.Forward );

				entity.TraceAttack( Player, HalfLifeDll.SkillData.PlrDmgCrowbar, Globals.Forward, trace, DamageClub );
			}
			else
			{
				ScenePhysicsDamage.MeleeImpact( trace, HalfLifeDll.SkillData.PlrDmgCrowbar / 2f, Globals.Forward );

				entity.TraceAttack( Player, HalfLifeDll.SkillData.PlrDmgCrowbar / 2f, Globals.Forward, trace, DamageClub );
			}

			Combat.ApplyMultiDamage( Player, Player );

			var volume = 1f;
			var hitWorld = true;

			if ( entity is not null && entity.Classify() != ClassNone && entity.Classify() != ClassMachine )
			{
				switch ( Server.RandomLong( 0, 2 ) )
				{
					case 0:
						Sound.Emit( Player, SoundChannel.Item, "weapons/cbar_hitbod1.wav", 1f, Server.AttenuationNormal );
						break;
					case 1:
						Sound.Emit( Player, SoundChannel.Item, "weapons/cbar_hitbod2.wav", 1f, Server.AttenuationNormal );
						break;
					case 2:
						Sound.Emit( Player, SoundChannel.Item, "weapons/cbar_hitbod3.wav", 1f, Server.AttenuationNormal );
						break;
				}

				Player.WeaponVolume = BodyHitVolume;

				if ( !entity.IsAlive )
					return true;

				volume = 0.1f;
				hitWorld = false;
			}

			if ( hitWorld )
			{
				var volumeBar = TextureType.PlaySound( trace, source, source + ((end - source) * 2f), Bullet.PlayerCrowbar );

				if ( HalfLifeDll.GameRules.IsMultiplayer() )
					volumeBar = 1f;

				switch ( Server.RandomLong( 0, 1 ) )
				{
					case 0:
						Sound.EmitDyn( Player, SoundChannel.Item, "weapons/cbar_hit1.wav", volumeBar, Server.AttenuationNormal, 0, 98 + Server.RandomLong( 0, 3 ) );
						break;
					case 1:
						Sound.EmitDyn( Player, SoundChannel.Item, "weapons/cbar_hit2.wav", volumeBar, Server.AttenuationNormal, 0, 98 + Server.RandomLong( 0, 3 ) );
						break;
				}

				_hitTrace = trace;
			}

			Player.WeaponVolume = (int)(volume * WallHitVolume);
		}

		NextPrimaryAttack = GetNextAttackDelay( 0.25f );

		SetThink( Smack );
		NextThink = (float)(Globals.Time + 0.2);

		return !ClientSide;
	}
}
