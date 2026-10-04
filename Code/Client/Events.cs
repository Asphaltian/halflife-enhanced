using GoldSrc;
using GoldSrc.Bsp;

namespace HalfLife;

partial class HalfLifeClientDll
{
	private const int CrowbarAttack1Miss = 4;
	private const int CrowbarAttack2Miss = 5;
	private const int CrowbarAttack3Miss = 7;

	private const int GlockShoot = 3;
	private const int GlockShootEmpty = 4;

	private const int ShotgunFire = 1;
	private const int ShotgunFire2 = 2;

	private const int PythonFire1 = 2;

	private const int CrossbowFire1 = 4;
	private const int CrossbowFire3 = 6;

	private const int RpgFire2 = 3;

	private const int HornetGunShoot = 5;

	private const int GaussFire2 = 6;

	private const int EgonFireModeWide = 1;

	private static readonly int[] EgonFireAnims = [5, 6, 7, 8];

	private const int TripmineDraw = 6;

	private const int SqueakThrow = 5;

	private const int Mp5Launch = 2;
	private const int Mp5Fire1 = 5;

	private const int SoundChangePitch = 1 << 7;

	private const float DefaultViewHeight = 28f;
	private const float DuckView = 12f;
	private const int MaxTextureName = 13;

	private readonly int[] _tracerCount = new int[32];

	private int _swing;
	private GoldSrc.Beam _egonBeam;
	private GoldSrc.Beam _egonBeam2;

	private void HookEvents()
	{
		HookEvent( "events/glock1.sc", FireGlock1 );
		HookEvent( "events/glock2.sc", FireGlock2 );
		HookEvent( "events/shotgun1.sc", FireShotGunSingle );
		HookEvent( "events/shotgun2.sc", FireShotGunDouble );
		HookEvent( "events/mp5.sc", FireMp5 );
		HookEvent( "events/mp52.sc", FireMp52 );
		HookEvent( "events/python.sc", FirePython );
		HookEvent( "events/crowbar.sc", Crowbar );
		HookEvent( "events/crossbow1.sc", FireCrossbow );
		HookEvent( "events/crossbow2.sc", FireCrossbow2 );
		HookEvent( "events/rpg.sc", FireRpg );
		HookEvent( "events/firehornet.sc", HornetGunFire );
		HookEvent( "events/gauss.sc", FireGauss );
		HookEvent( "events/gaussspin.sc", SpinGauss );
		HookEvent( "events/egon_fire.sc", EgonFire );
		HookEvent( "events/egon_stop.sc", EgonStop );
		HookEvent( "events/tripfire.sc", TripmineFire );
		HookEvent( "events/snarkfire.sc", SnarkFire );
		HookEvent( "events/train.sc", TrainPitchAdjust );
	}

	private bool IsFirstPersonSpec => User1 == HudSpectator.ObsInEye || (User1 != 0 && HudSpectator.Pip == HudSpectator.InsetInEye);

	private bool IsLocal( int index ) => IsFirstPersonSpec ? User2 == index : Engine.IsLocal( index - 1 );

	private void MuzzleFlash()
	{
		Engine.ViewModel.Effects |= Effects.MuzzleFlash;
	}

	private bool IsPlayer( int index ) => index >= 1 && index <= Engine.MaxClients;

	private Vector3 GetGunPosition( in EventArgs args, Vector3 origin )
	{
		var viewOffset = new Vector3( 0f, 0f, DefaultViewHeight );

		if ( IsPlayer( args.EntIndex ) )
		{
			if ( IsLocal( args.EntIndex ) )
				viewOffset = Engine.LocalPlayerViewHeight;
			else if ( args.Ducking == 1 )
				viewOffset.z = DuckView;
		}

		return origin + viewOffset;
	}

	private void GetDefaultShellInfo( in EventArgs args, Vector3 origin, Vector3 velocity, out Vector3 shellVelocity, out Vector3 shellOrigin, Vector3 forward, Vector3 right, Vector3 up, float forwardScale, float upScale, float rightScale )
	{
		var viewOffset = new Vector3( 0f, 0f, DefaultViewHeight );

		if ( IsPlayer( args.EntIndex ) )
		{
			if ( IsLocal( args.EntIndex ) )
				viewOffset = Engine.LocalPlayerViewHeight;
			else if ( args.Ducking == 1 )
				viewOffset.z = DuckView;
		}

		var fR = Server.RandomFloat( 50f, 70f );
		var fU = Server.RandomFloat( 100f, 150f );

		shellVelocity = velocity + (right * fR) + (up * fU) + (forward * 25f);
		shellOrigin = origin + viewOffset + (up * upScale) + (forward * forwardScale) + (right * rightScale);
	}

	private void EjectBrass( Vector3 origin, Vector3 velocity, float rotation, int model, int soundType )
	{
		Engine.TempModel( origin, velocity, new Vector3( 0f, rotation, 0f ), 2.5f, model, soundType );
	}

	private float PlayTextureSound( int index, in PmTrace trace, Vector3 source, Vector3 end, Bullet bulletType )
	{
		var entity = Engine.IndexFromTrace( trace );
		var textureType = (char)0;

		if ( entity == 0 )
		{
			if ( Engine.TraceTexture( trace.Ent, source, end ) is { } textureName )
			{
				var start = 0;

				if ( textureName.StartsWith( '-' ) || textureName.StartsWith( '+' ) )
					start += 2;

				if ( start < textureName.Length && "{!~ ".Contains( textureName[start] ) )
					start++;

				textureName = start < textureName.Length ? textureName[start..] : string.Empty;

				if ( textureName.Length > MaxTextureName - 1 )
					textureName = textureName[..(MaxTextureName - 1)];

				textureType = _movement.FindTextureType( textureName );
			}
		}
		else if ( Engine.GetEntityByIndex( entity ) is { } hit && (hit.EFlags & EntityState.EFlagFleshSound) != 0 )
		{
			textureType = TextureType.Flesh;
		}

		float volume;
		float volumeBar;
		string[] sounds;
		var attenuation = Server.AttenuationNormal;

		switch ( textureType )
		{
			default:
				volume = 0.9f;
				volumeBar = 0.6f;
				sounds = ["player/pl_step1.wav", "player/pl_step2.wav"];
				break;
			case TextureType.Metal:
				volume = 0.9f;
				volumeBar = 0.3f;
				sounds = ["player/pl_metal1.wav", "player/pl_metal2.wav"];
				break;
			case TextureType.Dirt:
				volume = 0.9f;
				volumeBar = 0.1f;
				sounds = ["player/pl_dirt1.wav", "player/pl_dirt2.wav", "player/pl_dirt3.wav"];
				break;
			case TextureType.Vent:
				volume = 0.5f;
				volumeBar = 0.3f;
				sounds = ["player/pl_duct1.wav", "player/pl_duct1.wav"];
				break;
			case TextureType.Grate:
				volume = 0.9f;
				volumeBar = 0.5f;
				sounds = ["player/pl_grate1.wav", "player/pl_grate4.wav"];
				break;
			case TextureType.Tile:
				volume = 0.8f;
				volumeBar = 0.2f;
				sounds = ["player/pl_tile1.wav", "player/pl_tile3.wav", "player/pl_tile2.wav", "player/pl_tile4.wav"];
				break;
			case TextureType.Slosh:
				volume = 0.9f;
				volumeBar = 0f;
				sounds = ["player/pl_slosh1.wav", "player/pl_slosh3.wav", "player/pl_slosh2.wav", "player/pl_slosh4.wav"];
				break;
			case TextureType.Wood:
				volume = 0.9f;
				volumeBar = 0.2f;
				sounds = ["debris/wood1.wav", "debris/wood2.wav", "debris/wood3.wav"];
				break;
			case TextureType.Glass:
			case TextureType.Computer:
				volume = 0.8f;
				volumeBar = 0.2f;
				sounds = ["debris/glass1.wav", "debris/glass2.wav", "debris/glass3.wav"];
				break;
			case TextureType.Flesh:
				if ( bulletType == Bullet.PlayerCrowbar )
					return 0f;

				volume = 1f;
				volumeBar = 0.2f;
				sounds = ["weapons/bullet_hit1.wav", "weapons/bullet_hit2.wav"];
				attenuation = 1f;
				break;
		}

		Engine.PlaySound( 0, trace.EndPos, SoundChannel.Static, sounds[Server.RandomLong( 0, sounds.Length - 1 )], volume, attenuation, 0, 96 + Server.RandomLong( 0, 0xf ) );

		return volumeBar;
	}

	private static string DamageDecal( PhysEnt physEnt )
	{
		if ( physEnt.ClassNumber == 1 )
			return $"{{break{Server.RandomLong( 0, 2 ) + 1}";

		if ( physEnt.RenderMode != RenderMode.Normal )
			return "{bproof1";

		return $"{{shot{Server.RandomLong( 0, 4 ) + 1}";
	}

	private void GunshotDecalTrace( in PmTrace trace, string decalName )
	{
		Engine.BulletImpactParticles( trace.EndPos );

		var random = Server.RandomLong( 0, 0x7FFF );

		if ( random < 0x7fff / 2 )
			Engine.PlaySound( -1, trace.EndPos, SoundChannel.Auto, $"weapons/ric{(random % 5) + 1}.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		var physEnt = Engine.GetPhysEnt( trace.Ent );

		if ( !string.IsNullOrEmpty( decalName ) && physEnt is not null && (physEnt.Solid == Solid.Bsp || physEnt.MoveType == MoveType.PushStep) && GoldSrc.Client.DecalLimit != 0f )
			Engine.DecalShoot( Engine.DecalIndexFromName( decalName ), Engine.IndexFromTrace( trace ), 0, trace.EndPos, default );
	}

	private void DecalGunshot( in PmTrace trace )
	{
		if ( Engine.GetPhysEnt( trace.Ent ) is { Solid: Solid.Bsp } physEnt )
			GunshotDecalTrace( trace, DamageDecal( physEnt ) );
	}

	private bool CheckTracer( int index, Vector3 source, Vector3 end, Vector3 forward, Vector3 right, int tracerFrequency, ref int tracerCount )
	{
		if ( tracerFrequency == 0 || tracerCount++ % tracerFrequency != 0 )
			return false;

		var tracerSource = IsPlayer( index ) ? source + new Vector3( 0f, 0f, -4f ) + (right * 2f) + (forward * 16f) : source;

		Engine.TracerEffect( tracerSource, end );

		return tracerFrequency != 1;
	}

	private void FireBullets( int index, Vector3 forward, Vector3 right, Vector3 up, int shots, Vector3 source, Vector3 directionShooting, float distance, Bullet bulletType, int tracerFrequency, ref int tracerCount, float spreadX, float spreadY )
	{
		for ( var shot = 1; shot <= shots; shot++ )
		{
			Vector3 direction;

			if ( bulletType == Bullet.PlayerBuckshot )
			{
				float x, y, z;

				do
				{
					x = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
					y = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
					z = (x * x) + (y * y);
				}
				while ( z > 1f );

				direction = directionShooting + (x * spreadX * right) + (y * spreadY * up);
			}
			else
			{
				direction = directionShooting + (spreadX * right) + (spreadY * up);
			}

			var end = source + (distance * direction);

			Engine.PushPMStates();
			Engine.SetSolidPlayers( index - 1 );
			Engine.SetTraceHull( 2 );

			var trace = Engine.PlayerTrace( source, end, TraceFlags.Normal, -1 );
			var tracer = CheckTracer( index, source, trace.EndPos, forward, right, tracerFrequency, ref tracerCount );

			if ( trace.Fraction != 1f )
			{
				switch ( bulletType )
				{
					default:
						PlayTextureSound( index, trace, source, end, bulletType );
						DecalGunshot( trace );
						break;
					case Bullet.PlayerMp5:
						if ( !tracer )
						{
							PlayTextureSound( index, trace, source, end, bulletType );
							DecalGunshot( trace );
						}

						break;
					case Bullet.PlayerBuckshot:
						DecalGunshot( trace );
						break;
				}
			}

			Engine.PopPMStates();
		}
	}

	private void FireShotGunDouble( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;

		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		var shell = Engine.FindModelIndex( "models/shotgunshell.mdl" );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( ShotgunFire2, 2 );
			PunchAxis( 0, -10f );
		}

		for ( var i = 0; i < 2; i++ )
		{
			GetDefaultShellInfo( args, origin, args.Velocity, out var shellVelocity, out var shellOrigin, forward, right, up, 32f, -12f, 6f );
			EjectBrass( shellOrigin, shellVelocity, angles.y, shell, TempEntity.BounceShotShell );
		}

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/dbarrel1.wav", Server.RandomFloat( 0.98f, 1f ), Server.AttenuationNormal, 0, 85 + Server.RandomLong( 0, 0x1f ) );

		var source = GetGunPosition( args, origin );

		if ( Engine.MaxClients > 1 )
			FireBullets( index, forward, right, up, 8, source, forward, 2048f, Bullet.PlayerBuckshot, 0, ref _tracerCount[index - 1], 0.17365f, 0.04362f );
		else
			FireBullets( index, forward, right, up, 12, source, forward, 2048f, Bullet.PlayerBuckshot, 0, ref _tracerCount[index - 1], 0.08716f, 0.08716f );
	}

	private void FireShotGunSingle( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;

		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		var shell = Engine.FindModelIndex( "models/shotgunshell.mdl" );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( ShotgunFire, 2 );
			PunchAxis( 0, -5f );
		}

		GetDefaultShellInfo( args, origin, args.Velocity, out var shellVelocity, out var shellOrigin, forward, right, up, 32f, -12f, 6f );
		EjectBrass( shellOrigin, shellVelocity, angles.y, shell, TempEntity.BounceShotShell );

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/sbarrel1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0x1f ) );

		var source = GetGunPosition( args, origin );

		if ( Engine.MaxClients > 1 )
			FireBullets( index, forward, right, up, 4, source, forward, 2048f, Bullet.PlayerBuckshot, 0, ref _tracerCount[index - 1], 0.08716f, 0.04362f );
		else
			FireBullets( index, forward, right, up, 6, source, forward, 2048f, Bullet.PlayerBuckshot, 0, ref _tracerCount[index - 1], 0.08716f, 0.08716f );
	}

	private void FireMp5( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;

		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		var shell = Engine.FindModelIndex( "models/shell.mdl" );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( Mp5Fire1 + Server.RandomLong( 0, 2 ), 2 );
			PunchAxis( 0, Server.RandomFloat( -2f, 2f ) );
		}

		GetDefaultShellInfo( args, origin, args.Velocity, out var shellVelocity, out var shellOrigin, forward, right, up, 20f, -12f, 4f );
		EjectBrass( shellOrigin, shellVelocity, angles.y, shell, TempEntity.BounceShell );

		var sample = Server.RandomLong( 0, 1 ) == 0 ? "weapons/hks1.wav" : "weapons/hks2.wav";

		Engine.PlaySound( index, origin, SoundChannel.Weapon, sample, 1f, Server.AttenuationNormal, 0, 94 + Server.RandomLong( 0, 0xf ) );

		FireBullets( index, forward, right, up, 1, GetGunPosition( args, origin ), forward, 8192f, Bullet.PlayerMp5, 2, ref _tracerCount[index - 1], args.FParam1, args.FParam2 );
	}

	private void FireMp52( EventArgs args )
	{
		var index = args.EntIndex;

		if ( IsLocal( index ) )
		{
			Engine.WeaponAnimation( Mp5Launch, 2 );
			PunchAxis( 0, -10f );
		}

		var sample = Server.RandomLong( 0, 1 ) == 0 ? "weapons/glauncher.wav" : "weapons/glauncher2.wav";

		Engine.PlaySound( index, args.Origin, SoundChannel.Weapon, sample, 1f, Server.AttenuationNormal, 0, 94 + Server.RandomLong( 0, 0xf ) );
	}

	private void FireCrossbow( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/xbow_fire1.wav", 1f, Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0xF ) );
		Engine.PlaySound( index, origin, SoundChannel.Item, "weapons/xbow_reload1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0xF ) );

		if ( !IsLocal( index ) )
			return;

		if ( args.IParam1 != 0 )
			Engine.WeaponAnimation( CrossbowFire1, 1 );
		else if ( args.IParam2 != 0 )
			Engine.WeaponAnimation( CrossbowFire3, 1 );

		PunchAxis( 0, -2f );
	}

	private static void BoltCallback( TempEnt entity, float frameTime, float currentTime )
	{
		entity.Origin = entity.BaselineVUser1;
		entity.Angles = entity.BaselineVUser2;
	}

	private void FireCrossbow2( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;

		MathLib.AngleVectors( angles, out var forward, out _, out _ );

		var source = GetGunPosition( args, origin );
		var end = source + (8192f * forward);

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/xbow_fire1.wav", 1f, Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0xF ) );
		Engine.PlaySound( index, origin, SoundChannel.Item, "weapons/xbow_reload1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, 93 + Server.RandomLong( 0, 0xF ) );

		if ( IsLocal( index ) )
		{
			if ( args.IParam1 != 0 )
				Engine.WeaponAnimation( CrossbowFire1, 1 );
			else if ( args.IParam2 != 0 )
				Engine.WeaponAnimation( CrossbowFire3, 1 );
		}

		Engine.PushPMStates();
		Engine.SetSolidPlayers( index - 1 );
		Engine.SetTraceHull( 2 );

		var trace = Engine.PlayerTrace( source, end, TraceFlags.Normal, -1 );

		if ( trace.Fraction < 1f )
		{
			var physEnt = Engine.GetPhysEnt( trace.Ent );

			if ( physEnt.Solid != Solid.Bsp )
			{
				switch ( Server.RandomLong( 0, 1 ) )
				{
					case 0:
						Engine.PlaySound( index, trace.EndPos, SoundChannel.Body, "weapons/xbow_hitbod1.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
						break;

					case 1:
						Engine.PlaySound( index, trace.EndPos, SoundChannel.Body, "weapons/xbow_hitbod2.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );
						break;
				}
			}
			else if ( physEnt.RenderMode == RenderMode.Normal )
			{
				Engine.PlaySound( 0, trace.EndPos, SoundChannel.Body, "weapons/xbow_hit1.wav", Server.RandomFloat( 0.95f, 1f ), Server.AttenuationNormal, 0, Server.PitchNormal );

				if ( Engine.PointContents( trace.EndPos ) != (int)Contents.Water )
					Engine.SparkShower( trace.EndPos );

				var boltModel = Engine.FindModelIndex( "models/crossbow_bolt.mdl" );
				var boltAngles = MathLib.VectorAngles( forward );
				var bolt = Engine.TempModel( trace.EndPos - (forward * 10f), Vector3.Zero, boltAngles, 5f, boltModel, TempEntity.BounceNull );

				if ( bolt is not null )
				{
					bolt.Flags |= TempEntityFlags.ClientCustom;
					bolt.BaselineVUser1 = trace.EndPos - (forward * 10f);
					bolt.BaselineVUser2 = boltAngles;
					bolt.Callback = BoltCallback;
				}
			}
		}

		Engine.PopPMStates();
	}

	private void FireRpg( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/rocketfire1.wav", 0.9f, Server.AttenuationNormal, 0, Server.PitchNormal );
		Engine.PlaySound( index, origin, SoundChannel.Item, "weapons/glauncher.wav", 0.7f, Server.AttenuationNormal, 0, Server.PitchNormal );

		if ( !IsLocal( index ) )
			return;

		Engine.WeaponAnimation( RpgFire2, 1 );

		PunchAxis( 0, -5f );
	}

	private void HornetGunFire( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;

		if ( IsLocal( index ) )
		{
			PunchAxis( 0, Server.RandomLong( 0, 2 ) );
			Engine.WeaponAnimation( HornetGunShoot, 1 );
		}

		Engine.PlaySound( index, origin, SoundChannel.Weapon, $"agrunt/ag_fire{Server.RandomLong( 0, 2 ) + 1}.wav", 1f, Server.AttenuationNormal, 0, 100 );
	}

	private void SpinGauss( EventArgs args )
	{
		Engine.PlaySound( args.EntIndex, args.Origin, SoundChannel.Weapon, "ambience/pulsemachine.wav", 1f, Server.AttenuationNormal, args.BParam1 != 0 ? SoundChangePitch : 0, args.IParam1 );
	}

	private void StopPreviousGauss( int index )
	{
		Engine.KillEvents( index, "events/gaussspin.sc" );
		Engine.StopSound( index, SoundChannel.Weapon, "ambience/pulsemachine.wav" );
	}

	private void FireGauss( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var damage = args.FParam1;
		var primaryFire = args.BParam1 != 0;
		var hasPunched = false;
		var firstBeam = true;
		var maxHits = 10;

		if ( args.BParam2 != 0 )
		{
			StopPreviousGauss( index );
			return;
		}

		var source = GetGunPosition( args, origin );

		var beam = Engine.FindModelIndex( "sprites/smoke.spr" );
		var glow = Engine.FindModelIndex( "sprites/hotglow.spr" );

		MathLib.AngleVectors( args.Angles, out var forward, out _, out _ );

		var destination = source + (forward * 8192f);

		if ( IsLocal( index ) )
		{
			PunchAxis( 0, -2f );
			Engine.WeaponAnimation( GaussFire2, 2 );

			if ( !primaryFire )
				ApplyVelocity = damage;
		}

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/gauss2.wav", (float)(0.5 + (damage * (1.0 / 400.0))), Server.AttenuationNormal, 0, 85 + Server.RandomLong( 0, 0x1f ) );

		while ( damage > 10f && maxHits > 0 )
		{
			maxHits--;

			Engine.PushPMStates();
			Engine.SetSolidPlayers( index - 1 );
			Engine.SetTraceHull( 2 );

			var trace = Engine.PlayerTrace( source, destination, TraceFlags.Normal, -1 );

			Engine.PopPMStates();

			if ( trace.AllSolid )
				break;

			var width = primaryFire ? 1f : 2.5f;
			var brightness = primaryFire ? 128f : damage;
			var green = primaryFire ? 128f : 255f;
			var blue = primaryFire ? 0f : 255f;

			if ( firstBeam )
			{
				if ( IsLocal( index ) )
					MuzzleFlash();

				firstBeam = false;

				Engine.BeamEntPoint( index | 0x1000, trace.EndPos, beam, 0.1f, width, 0f, brightness, 0f, 0, 0f, 255f, green, blue );
			}
			else
			{
				Engine.BeamPoints( source, trace.EndPos, beam, 0.1f, width, 0f, brightness, 0f, 0, 0f, 255f, green, blue );
			}

			if ( Engine.GetPhysEnt( trace.Ent ) is not { } physEnt )
				break;

			if ( physEnt.Solid != Solid.Bsp )
			{
				source = trace.EndPos + forward;
				continue;
			}

			var n = -Vector3.Dot( trace.PlaneNormal, forward );

			if ( n < 0.5 )
			{
				forward += 2f * n * trace.PlaneNormal;

				source = trace.EndPos + (8f * forward);
				destination = source + (8192f * forward);

				Engine.TempSprite( trace.EndPos, Vector3.Zero, 0.2f, glow, RenderMode.Glow, RenderFx.NoDissipation, (float)(damage * n / 255.0), (float)(damage * n * 0.5 * 0.1), TempEntityFlags.FadeOut );

				Engine.SpriteTrail( TempEntity.SpriteTrail, trace.EndPos, trace.EndPos + trace.PlaneNormal, glow, 3, 0.1f, (float)(Server.RandomFloat( 10f, 20f ) / 100.0), 100f, 255, 100f );

				if ( n == 0f )
					n = 0.1f;

				damage *= 1f - n;
				continue;
			}

			DecalGunshot( trace );

			Engine.TempSprite( trace.EndPos, Vector3.Zero, 1f, glow, RenderMode.Glow, RenderFx.NoDissipation, (float)(damage / 255.0), 6f, TempEntityFlags.FadeOut );

			if ( hasPunched )
				break;

			hasPunched = true;

			if ( primaryFire )
			{
				Engine.TempSprite( trace.EndPos, Vector3.Zero, 0.2f, glow, RenderMode.Glow, RenderFx.NoDissipation, (float)(200.0 / 255.0), 0.3f, TempEntityFlags.FadeOut );

				Engine.SpriteTrail( TempEntity.SpriteTrail, trace.EndPos, trace.EndPos + trace.PlaneNormal, glow, 8, 0.6f, (float)(Server.RandomFloat( 10f, 20f ) / 100.0), 100f, 255, 200f );

				damage = 0f;
				continue;
			}

			Engine.PushPMStates();
			Engine.SetSolidPlayers( index - 1 );
			Engine.SetTraceHull( 2 );

			var beamTrace = Engine.PlayerTrace( trace.EndPos + (8f * forward), destination, TraceFlags.Normal, -1 );

			if ( beamTrace.AllSolid )
			{
				damage = 0f;
			}
			else
			{
				beamTrace = Engine.PlayerTrace( beamTrace.EndPos, trace.EndPos, TraceFlags.Normal, -1 );

				var thickness = MathLib.Length( beamTrace.EndPos - trace.EndPos );

				if ( thickness < damage )
				{
					if ( thickness == 0f )
						thickness = 1f;

					damage -= thickness;

					Engine.SpriteTrail( TempEntity.SpriteTrail, trace.EndPos, trace.EndPos - forward, glow, 3, 0.1f, (float)(Server.RandomFloat( 10f, 20f ) / 100.0), 100f, 255, 100f );

					DecalGunshot( beamTrace );

					Engine.TempSprite( beamTrace.EndPos, Vector3.Zero, 0.1f, glow, RenderMode.Glow, RenderFx.NoDissipation, (float)(damage / 255.0), 6f, TempEntityFlags.FadeOut );

					Engine.SpriteTrail( TempEntity.SpriteTrail, beamTrace.EndPos, beamTrace.EndPos - forward, glow, (int)(damage * 0.3), 0.1f, (float)(Server.RandomFloat( 10f, 20f ) / 100.0), 200f, 255, 40f );

					source = beamTrace.EndPos + forward;
				}
			}

			Engine.PopPMStates();
		}
	}

	private void EgonFire( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var wide = args.IParam2 == EgonFireModeWide;
		var startup = args.BParam1;

		if ( startup != 0 )
			Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/egon_windup2.wav", wide ? 0.98f : 0.9f, Server.AttenuationNormal, 0, wide ? 125 : 100 );
		else
			Engine.PlaySound( index, origin, SoundChannel.Static, "weapons/egon_run3.wav", wide ? 0.98f : 0.9f, Server.AttenuationNormal, 0, wide ? 125 : 100 );

		if ( IsLocal( index ) )
			Engine.WeaponAnimation( EgonFireAnims[Server.RandomLong( 0, 3 )], 1 );

		if ( startup != 1 || !IsLocal( index ) || _egonBeam is not null || _egonBeam2 is not null )
			return;

		if ( Engine.GetEntityByIndex( index ) is not { } player )
			return;

		MathLib.AngleVectors( _hud.Angles, out var forward, out _, out _ );

		var source = GetGunPosition( args, player.Origin );

		Engine.PushPMStates();
		Engine.SetSolidPlayers( index - 1 );
		Engine.SetTraceHull( 2 );

		var trace = Engine.PlayerTrace( source, source + (forward * 2048f), TraceFlags.Normal, -1 );

		Engine.PopPMStates();

		var beamModel = Engine.FindModelIndex( "sprites/xbeam1.spr" );

		_egonBeam = Engine.BeamEntPoint( index | 0x1000, trace.EndPos, beamModel, 99999f, 3.5f, 0.2f, 0.7f, 55f, 0, 0f, 0.5f, 0.5f, 125f );

		if ( _egonBeam is not null )
			_egonBeam.Flags |= BeamFlags.SineNoise;

		_egonBeam2 = Engine.BeamEntPoint( index | 0x1000, trace.EndPos, beamModel, 99999f, 5f, 0.08f, 0.7f, 25f, 0, 0f, 0.5f, 0.5f, 125f );
	}

	private void EgonStop( EventArgs args )
	{
		var index = args.EntIndex;

		Engine.StopSound( index, SoundChannel.Static, "weapons/egon_run3.wav" );

		if ( args.IParam1 != 0 )
			Engine.PlaySound( index, args.Origin, SoundChannel.Weapon, "weapons/egon_off1.wav", 0.98f, Server.AttenuationNormal, 0, 100 );

		if ( !IsLocal( index ) )
			return;

		if ( _egonBeam is not null )
		{
			_egonBeam.Die = 0f;
			_egonBeam = null;
		}

		if ( _egonBeam2 is not null )
		{
			_egonBeam2.Die = 0f;
			_egonBeam2 = null;
		}
	}

	public void ClearEgonBeams()
	{
		_egonBeam = null;
		_egonBeam2 = null;
	}

	private void GameAddObjects()
	{
		if ( _egonBeam is not null && _egonBeam2 is not null )
			UpdateBeams();
	}

	private void UpdateBeams()
	{
		var player = Engine.GetLocalPlayer();
		var index = player.Index;

		MathLib.AngleVectors( Engine.ViewAngles, out var forward, out _, out _ );

		var source = LastOrigin;

		Engine.SetUpPlayerPrediction( true );

		Engine.PushPMStates();
		Engine.SetSolidPlayers( index - 1 );
		Engine.SetTraceHull( 2 );

		var trace = Engine.PlayerTrace( source, source + (forward * 2048f), TraceFlags.StudioBox, -1 );

		Engine.PopPMStates();

		_egonBeam.Target = trace.EndPos;
		_egonBeam.Die = (float)(Engine.ClientTime + 0.1);

		_egonBeam2.Target = trace.EndPos;
		_egonBeam2.Die = (float)(Engine.ClientTime + 0.1);
	}

	private void TripmineFire( EventArgs args )
	{
		var index = args.EntIndex;
		var source = args.Origin;

		MathLib.AngleVectors( args.Angles, out var forward, out _, out _ );

		if ( !IsLocal( index ) )
			return;

		source += Engine.LocalPlayerViewHeight;

		Engine.PushPMStates();
		Engine.SetSolidPlayers( index - 1 );
		Engine.SetTraceHull( 2 );

		var trace = Engine.PlayerTrace( source, source + (forward * 128f), TraceFlags.Normal, -1 );

		if ( trace.Fraction < 1.0 )
			Engine.WeaponAnimation( TripmineDraw, 0 );

		Engine.PopPMStates();
	}

	private void SnarkFire( EventArgs args )
	{
		var index = args.EntIndex;
		var source = args.Origin;

		MathLib.AngleVectors( args.Angles, out var forward, out _, out _ );

		if ( !IsLocal( index ) )
			return;

		if ( args.Ducking != 0 )
			source -= new Vector3( -16f, -16f, -36f ) - new Vector3( -16f, -16f, -18f );

		Engine.PushPMStates();
		Engine.SetSolidPlayers( index - 1 );
		Engine.SetTraceHull( 2 );

		var trace = Engine.PlayerTrace( source + (forward * 20f), source + (forward * 64f), TraceFlags.Normal, -1 );

		if ( !trace.AllSolid && !trace.StartSolid && trace.Fraction > 0.25 )
			Engine.WeaponAnimation( SqueakThrow, 0 );

		Engine.PopPMStates();
	}

	private void FirePython( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;

		MathLib.AngleVectors( args.Angles, out var forward, out var right, out var up );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( PythonFire1, Engine.MaxClients == 1 ? 0 : 1 );
			PunchAxis( 0, -10f );
		}

		var sample = Server.RandomLong( 0, 1 ) == 0 ? "weapons/357_shot1.wav" : "weapons/357_shot2.wav";

		Engine.PlaySound( index, origin, SoundChannel.Weapon, sample, Server.RandomFloat( 0.8f, 0.9f ), Server.AttenuationNormal, 0, Server.PitchNormal );

		var tracerCount = 0;

		FireBullets( index, forward, right, up, 1, GetGunPosition( args, origin ), forward, 8192f, Bullet.Player357, 0, ref tracerCount, args.FParam1, args.FParam2 );
	}

	private void FireGlock1( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;
		var empty = args.BParam1 != 0;

		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		var shell = Engine.FindModelIndex( "models/shell.mdl" );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( empty ? GlockShootEmpty : GlockShoot, 2 );
			PunchAxis( 0, -2f );
		}

		GetDefaultShellInfo( args, origin, args.Velocity, out var shellVelocity, out var shellOrigin, forward, right, up, 20f, -12f, 4f );
		EjectBrass( shellOrigin, shellVelocity, angles.y, shell, TempEntity.BounceShell );

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/pl_gun3.wav", Server.RandomFloat( 0.92f, 1f ), Server.AttenuationNormal, 0, 98 + Server.RandomLong( 0, 3 ) );

		var tracerCount = 0;
		FireBullets( index, forward, right, up, 1, GetGunPosition( args, origin ), forward, 8192f, Bullet.Player9mm, 0, ref tracerCount, args.FParam1, args.FParam2 );
	}

	private void FireGlock2( EventArgs args )
	{
		var index = args.EntIndex;
		var origin = args.Origin;
		var angles = args.Angles;

		MathLib.AngleVectors( angles, out var forward, out var right, out var up );

		var shell = Engine.FindModelIndex( "models/shell.mdl" );

		if ( IsLocal( index ) )
		{
			MuzzleFlash();
			Engine.WeaponAnimation( GlockShoot, 2 );
			PunchAxis( 0, -2f );
		}

		GetDefaultShellInfo( args, origin, args.Velocity, out var shellVelocity, out var shellOrigin, forward, right, up, 20f, -12f, 4f );
		EjectBrass( shellOrigin, shellVelocity, angles.y, shell, TempEntity.BounceShell );

		Engine.PlaySound( index, origin, SoundChannel.Weapon, "weapons/pl_gun3.wav", Server.RandomFloat( 0.92f, 1f ), Server.AttenuationNormal, 0, 98 + Server.RandomLong( 0, 3 ) );

		FireBullets( index, forward, right, up, 1, GetGunPosition( args, origin ), forward, 8192f, Bullet.Player9mm, 0, ref _tracerCount[index - 1], args.FParam1, args.FParam2 );
	}

	private void TrainPitchAdjust( EventArgs args )
	{
		var parameters = (ushort)args.IParam1;
		var stop = args.BParam1 != 0;

		var volume = (parameters & 0x003f) / 40f;
		var noise = (parameters >> 12) & 0x0007;
		var pitch = (int)(10f * ((parameters >> 6) & 0x003f));

		string sample;

		switch ( noise )
		{
			case 1:
				sample = "plats/ttrain1.wav";
				break;
			case 2:
				sample = "plats/ttrain2.wav";
				break;
			case 3:
				sample = "plats/ttrain3.wav";
				break;
			case 4:
				sample = "plats/ttrain4.wav";
				break;
			case 5:
				sample = "plats/ttrain6.wav";
				break;
			case 6:
				sample = "plats/ttrain7.wav";
				break;
			default:
				return;
		}

		if ( stop )
			Engine.StopSound( args.EntIndex, SoundChannel.Static, sample );
		else
			Engine.PlaySound( args.EntIndex, args.Origin, SoundChannel.Static, sample, volume, Server.AttenuationNormal, Server.SoundChangePitch, pitch );
	}

	private void Crowbar( EventArgs args )
	{
		Engine.PlaySound( args.EntIndex, args.Origin, SoundChannel.Weapon, "weapons/cbar_miss1.wav", 1f, Server.AttenuationNormal, 0, Server.PitchNormal );

		if ( !IsLocal( args.EntIndex ) )
			return;

		Engine.WeaponAnimation( CrowbarAttack1Miss, 1 );

		switch ( _swing++ % 3 )
		{
			case 0:
				Engine.WeaponAnimation( CrowbarAttack1Miss, 1 );
				break;
			case 1:
				Engine.WeaponAnimation( CrowbarAttack2Miss, 1 );
				break;
			case 2:
				Engine.WeaponAnimation( CrowbarAttack3Miss, 1 );
				break;
		}
	}
}
