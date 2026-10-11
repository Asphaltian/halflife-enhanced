using System;

namespace GoldSrc;

partial class Client
{
	private readonly int[] _multiGunshotDecal = new int[1];

	private static bool BloodEnabled => Cvar.GetFloat( "violence_hblood" ) != 0f || Cvar.GetFloat( "violence_ablood" ) != 0f;

	private void ShootDecal( int decal, int entity, int modelIndex, Vector3 position, DecalFlags flags )
	{
		if ( entity >= _maxEdicts )
			throw new InvalidOperationException( $"Decal: entity = {entity}" );

		if ( DecalLimit != 0f )
			DecalShoot( decal, entity, modelIndex, position, flags );
	}

	private void ParseTempEntity( MessageReader message )
	{
		var type = message.ReadByte();

		switch ( type )
		{
			case TempEntity.BeamPoints:
			case TempEntity.BeamEntPoint:
			case TempEntity.BeamEnts:
				{
					var startEntity = 0;
					var endEntity = 0;
					var start = Vector3.Zero;
					var end = Vector3.Zero;

					if ( type == TempEntity.BeamEnts )
					{
						startEntity = message.ReadShort();
						endEntity = message.ReadShort();
					}
					else if ( type == TempEntity.BeamEntPoint )
					{
						startEntity = message.ReadShort();
						end = message.ReadPosition();
					}
					else
					{
						start = message.ReadPosition();
						end = message.ReadPosition();
					}

					var modelIndex = message.ReadShort();
					var startFrame = message.ReadByte();
					var frameRate = message.ReadByte() * 0.1f;
					var life = message.ReadByte() * 0.1f;
					var width = message.ReadByte() * 0.1f;
					var amplitude = message.ReadByte() * 0.01f;
					var r = message.ReadByte() * (1f / 255f);
					var g = message.ReadByte() * (1f / 255f);
					var b = message.ReadByte() * (1f / 255f);
					var brightness = message.ReadByte() * (1f / 255f);
					float speed = (int)(message.ReadByte() * 0.1);

					if ( type == TempEntity.BeamEnts )
						BeamEnts( startEntity, endEntity, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b );
					else if ( type == TempEntity.BeamEntPoint )
						BeamEntPoint( startEntity, end, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b );
					else
						BeamPoints( start, end, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b );

					break;
				}

			case TempEntity.Lightning:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();
					var life = message.ReadByte() * 0.1f;
					var width = message.ReadByte() * 0.1f;
					var amplitude = message.ReadByte() * 0.01f;
					var modelIndex = message.ReadShort();

					BeamLightning( start, end, modelIndex, life, width, amplitude, 0.6f, 3.5f );
					break;
				}

			case TempEntity.BeamTorus:
			case TempEntity.BeamDisk:
			case TempEntity.BeamCylinder:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var startFrame = message.ReadByte();
					var frameRate = message.ReadByte() * 0.1f;
					var life = message.ReadByte() * 0.1f;
					float width = message.ReadByte();
					var amplitude = message.ReadByte() * 0.01f;
					var r = message.ReadByte() * (1f / 255f);
					var g = message.ReadByte() * (1f / 255f);
					var b = message.ReadByte() * (1f / 255f);
					var brightness = message.ReadByte() * (1f / 255f);
					float speed = (int)(message.ReadByte() * 0.1);

					BeamCirclePoints( type, start, end, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b );
					break;
				}

			case TempEntity.BeamFollow:
				{
					var entity = message.ReadShort();
					var modelIndex = message.ReadShort();
					var life = message.ReadByte() * 0.1f;
					float width = message.ReadByte();
					var r = message.ReadByte() * (1f / 255f);
					var g = message.ReadByte() * (1f / 255f);
					var b = message.ReadByte() * (1f / 255f);
					var brightness = message.ReadByte() * (1f / 255f);

					BeamFollow( entity, modelIndex, life, width, r, g, b, brightness );
					break;
				}

			case TempEntity.BeamRing:
				{
					var startEntity = message.ReadShort();
					var endEntity = message.ReadShort();
					var modelIndex = message.ReadShort();
					var startFrame = message.ReadByte();
					var frameRate = message.ReadByte() * 0.1f;
					var life = message.ReadByte() * 0.1f;
					var width = message.ReadByte() * 0.1f;
					var amplitude = message.ReadByte() * 0.01f;
					var r = message.ReadByte() * (1f / 255f);
					var g = message.ReadByte() * (1f / 255f);
					var b = message.ReadByte() * (1f / 255f);
					var brightness = message.ReadByte() * (1f / 255f);
					float speed = (int)(message.ReadByte() * 0.1);

					BeamRing( startEntity, endEntity, modelIndex, life, width, amplitude, brightness, speed, startFrame, frameRate, r, g, b );
					break;
				}

			case TempEntity.KillBeam:
				BeamKill( message.ReadShort() );
				break;

			case TempEntity.Explosion:
				{
					var position = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var scale = message.ReadByte() * 0.1f;
					float frameRate = message.ReadByte();
					var flags = message.ReadByte();

					Explosion( position, modelIndex, scale, frameRate, flags );
					break;
				}

			case TempEntity.TarExplosion:
				{
					var position = message.ReadPosition();

					BlobExplosion( position );

					Sound.StartDynamicSound( -1, 0, Sound.PrecacheSound( ExplosionSoundNames[0] ), position, 1f, 1f, 0, 100 );
					break;
				}

			case TempEntity.Smoke:
				{
					var position = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var scale = message.ReadByte() * 0.1f;
					float frameRate = message.ReadByte();

					SpriteSmoke( DefaultSprite( position, modelIndex, frameRate ), scale );
					break;
				}

			case TempEntity.Gunshot:
				{
					var position = message.ReadPosition();

					RunParticleEffect( position, Vector3.Zero, 0, 20 );
					RicochetSound( position );
					break;
				}

			case TempEntity.Tracer:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();

					TracerEffect( start, end );
					break;
				}

			case TempEntity.Sparks:
				SparkEffect( message.ReadPosition(), 8, -200, 200 );
				break;

			case TempEntity.LavaSplash:
				LavaSplash( message.ReadPosition() );
				break;

			case TempEntity.Teleport:
				TeleportSplash( message.ReadPosition() );
				break;

			case TempEntity.Explosion2:
				{
					var position = message.ReadPosition();
					var colorStart = message.ReadByte();
					var colorLength = message.ReadByte();

					ParticleExplosion2( position, colorStart, colorLength );

					var light = AllocDlight( 0 );

					light.Origin = position;
					light.Radius = 350f;
					light.Die = (float)(ClientTime + 0.5);
					light.Decay = 300f;

					Sound.StartDynamicSound( -1, 0, Sound.PrecacheSound( ExplosionSoundNames[0] ), position, 1f, 0.6f, 0, 100 );
					break;
				}

			case TempEntity.SpriteTrail:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var count = message.ReadByte();
					var life = message.ReadByte() * 0.1f;
					float scale = message.ReadByte();

					scale = scale == 0f ? 1f : (float)(scale * 0.1);

					var amplitude = message.ReadByte() * 10f;
					float speed = (int)(message.ReadByte() * 10.0);

					SpriteTrail( type, start, end, modelIndex, count, life, scale, amplitude, 255, speed );
					break;
				}

			case TempEntity.BeamSprite:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();
					var beamModel = message.ReadShort();
					var spriteModel = message.ReadShort();

					BeamPoints( start, end, beamModel, 0.01f, 0.4f, 0f, Server.RandomFloat( 0.5f, 0.655f ), 5f, 0, 0f, 1f, 0f, 0f );
					TempSprite( end, Vector3.Zero, 0.1f, spriteModel, RenderMode.TransAdd, RenderFx.None, 0.35f, 0.01f, TempEntityFlags.None );
					break;
				}

			case TempEntity.GlowSprite:
				{
					var position = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var life = message.ReadByte() * 0.1f;
					var scale = message.ReadByte() * 0.1f;
					var alpha = message.ReadByte() * (1f / 255f);

					TempSprite( position, Vector3.Zero, scale, modelIndex, RenderMode.Glow, RenderFx.NoDissipation, alpha, life, TempEntityFlags.FadeOut );
					break;
				}

			case TempEntity.Line:
			case TempEntity.Box:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();
					var life = message.ReadShort() * 0.1f;
					var r = (byte)message.ReadByte();
					var g = (byte)message.ReadByte();
					var b = (byte)message.ReadByte();
					var color = (short)LookupColor( r, g, b );

					if ( type == TempEntity.Line )
					{
						ParticleLine( start, end, color, life );
						break;
					}

					var center = (end + start) * 0.5f;

					ParticleBox( start - center, end - center, center, color, life );
					break;
				}

			case TempEntity.StreakSplash:
				{
					var position = message.ReadPosition();
					var direction = message.ReadPosition();
					var color = message.ReadByte();
					var count = message.ReadShort();
					var speed = message.ReadShort();
					var velocityRange = message.ReadShort();

					StreakSplash( position, direction, color, count, speed, -velocityRange, velocityRange );
					break;
				}

			case TempEntity.LargeFunnel:
				{
					var position = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var flags = message.ReadShort();

					LargeFunnel( position, flags );
					FunnelSprite( position, modelIndex, flags );
					break;
				}

			case TempEntity.Fizz:
				{
					var entityIndex = message.ReadShort();
					var modelIndex = message.ReadShort();
					var density = message.ReadByte();

					if ( entityIndex >= _maxEdicts )
						throw new InvalidOperationException( $"Bubble: entity = {entityIndex}" );

					FizzEffect( GetEntity( entityIndex ), modelIndex, density );
					break;
				}

			case TempEntity.Implosion:
				{
					var position = message.ReadPosition();
					float radius = message.ReadByte();
					var count = message.ReadByte();
					var life = message.ReadByte() / 10f;

					Implosion( position, radius, count, life );
					break;
				}

			case TempEntity.BspDecal:
			case TempEntity.Decal:
			case TempEntity.WorldDecal:
			case TempEntity.WorldDecalHigh:
			case TempEntity.DecalHigh:
				{
					var position = message.ReadPosition();
					var modelIndex = 0;
					DecalFlags flags = default;
					int decal;
					int entity;

					if ( type == TempEntity.BspDecal )
					{
						flags = DecalFlags.Permanent;
						decal = message.ReadShort();
						entity = message.ReadShort();

						if ( entity != 0 )
							modelIndex = message.ReadShort();
					}
					else
					{
						decal = message.ReadByte();
						entity = type is TempEntity.Decal or TempEntity.DecalHigh ? message.ReadShort() : 0;

						if ( type is TempEntity.WorldDecalHigh or TempEntity.DecalHigh )
							decal += 256;
					}

					ShootDecal( decal, entity, modelIndex, position, flags );
					break;
				}

			case TempEntity.DLight:
				{
					var light = AllocDlight( 0 );

					light.Origin = message.ReadPosition();
					light.Radius = message.ReadByte() * 10;
					light.R = (byte)message.ReadByte();
					light.G = (byte)message.ReadByte();
					light.B = (byte)message.ReadByte();
					light.Die = (float)((message.ReadByte() * 0.1) + ClientTime);
					light.Decay = message.ReadByte() * 10;
					break;
				}

			case TempEntity.ELight:
				{
					var light = AllocElight( message.ReadShort() );

					light.Origin = message.ReadPosition();
					light.Radius = message.ReadCoord();
					light.R = (byte)message.ReadByte();
					light.G = (byte)message.ReadByte();
					light.B = (byte)message.ReadByte();

					var life = message.ReadByte() * 0.1f;

					light.Die = (float)(life + ClientTime);
					light.Decay = message.ReadCoord();

					if ( life != 0f )
						light.Decay /= life;

					break;
				}

			case TempEntity.BloodStream:
			case TempEntity.Blood:
				{
					var position = message.ReadPosition();
					var direction = message.ReadPosition();
					var color = message.ReadByte();
					var speed = message.ReadByte();

					if ( !BloodEnabled )
						break;

					if ( type == TempEntity.BloodStream )
						BloodStream( position, direction, color, speed );
					else
						Blood( position, direction, color, speed );

					break;
				}

			case TempEntity.Model:
				{
					var position = message.ReadPosition();
					var velocity = message.ReadPosition();
					var angles = new Vector3( 0f, message.ReadAngle(), 0f );
					var model = message.ReadShort();
					var sound = message.ReadByte();
					var life = message.ReadByte() * 0.1f;

					TempModel( position, velocity, angles, life, model, sound );
					break;
				}

			case TempEntity.GunshotDecal:
				{
					var position = message.ReadPosition();
					var entity = message.ReadShort();
					var decal = message.ReadByte();

					BulletImpactParticles( position );

					var random = Server.RandomLong( 0, 0x7fff );

					if ( random < 0x3fff )
						Sound.StartDynamicSound( -1, 0, Sound.PrecacheSound( RicochetSoundNames[random % 5] ), position, 1f, 1f, 0, 100 );

					ShootDecal( decal, entity, 0, position, default );
					break;
				}

			case TempEntity.ArmorRicochet:
				{
					var position = message.ReadPosition();
					var scale = message.ReadByte() * 0.1f;

					_ricochetSprite ??= ModelCache.ForName( "sprites/richo1.spr" );

					RicochetSprite( position, _ricochetSprite, 0.1f, scale );
					RicochetSound( position );
					break;
				}

			case TempEntity.PlayerDecal:
				{
					var player = message.ReadByte() - 1;
					var customization = PlayerCustomization( player );
					var position = message.ReadPosition();
					var entity = message.ReadShort();
					var decal = message.ReadByte();

					if ( entity >= _maxEdicts )
						throw new InvalidOperationException( $"Decal: entity = {entity}" );

					CustomDecalImage image = null;

					if ( customization is { Buffer: { } buffer, Info: { } wad, IsDecal: true, Translated: true } )
					{
						decal = Math.Min( decal, wad.LumpCount - 1 );

						customization.UserData1 = decal;

						image = wad.CacheGet( buffer, decal );
					}

					if ( DecalLimit == 0f )
						break;

					if ( customization is null )
						DecalShoot( decal, entity, 0, position, DecalFlags.Custom );
					else if ( image is not null )
						DecalShoot( ~player, entity, 0, position, DecalFlags.Custom );

					break;
				}

			case TempEntity.Sprite:
				{
					var position = message.ReadPosition();
					var model = message.ReadShort();
					var scale = message.ReadByte() * 0.1f;
					var alpha = message.ReadByte() * 0.003921569f;

					TempSprite( position, Vector3.Zero, scale, model, RenderMode.TransAdd, RenderFx.None, alpha, 0f, TempEntityFlags.SprAnimate );
					break;
				}

			case TempEntity.SpriteSpray:
				{
					var position = message.ReadPosition();
					var direction = message.ReadPosition();
					var model = message.ReadShort();
					var count = message.ReadByte();
					var speed = message.ReadByte();
					var noise = message.ReadByte();

					SpriteSpray( position, direction, model, count, speed * 2, noise );
					break;
				}

			case TempEntity.Bubbles:
			case TempEntity.BubbleTrail:
				{
					var mins = message.ReadPosition();
					var maxs = message.ReadPosition();
					var height = message.ReadCoord();
					var model = message.ReadShort();
					var count = message.ReadByte();
					var speed = message.ReadCoord();

					if ( type == TempEntity.Bubbles )
						Bubbles( mins, maxs, height, model, count, speed );
					else
						BubbleTrail( mins, maxs, height, model, count, speed );

					break;
				}

			case TempEntity.BloodSprite:
				{
					var position = message.ReadPosition();
					var spray = message.ReadShort();
					var drop = message.ReadShort();
					var color = message.ReadByte();
					float scale = message.ReadByte();

					if ( BloodEnabled )
						BloodSprite( position, color, spray, drop, scale );

					break;
				}

			case TempEntity.TextMessage:
				ParseTextMessage( message );
				break;

			case TempEntity.ShowLine:
				{
					var start = message.ReadPosition();
					var end = message.ReadPosition();

					ShowLine( start, end );
					break;
				}

			case TempEntity.KillPlayerAttachments:
				KillAttachedTents( message.ReadByte() );
				break;

			case TempEntity.ExplodeModel:
				{
					var position = message.ReadPosition();
					var speed = message.ReadCoord();
					var modelIndex = message.ReadShort();
					var count = message.ReadShort();
					var life = message.ReadByte() * 0.1f;

					TempSphereModel( position, speed, life, count, modelIndex );
					break;
				}

			case TempEntity.Projectile:
				{
					var position = message.ReadPosition();
					var velocity = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var life = message.ReadByte();
					var owner = message.ReadByte();

					Projectile( position, velocity, modelIndex, life, owner, null );
					break;
				}

			case TempEntity.Spray:
				{
					var position = message.ReadPosition();
					var direction = message.ReadPosition();
					var modelIndex = message.ReadShort();
					var count = message.ReadByte();
					var speed = message.ReadByte();
					var noise = message.ReadByte();
					var renderMode = (RenderMode)message.ReadByte();

					Spray( position, direction, modelIndex, count, speed, noise, renderMode );
					break;
				}

			case TempEntity.PlayerSprites:
				{
					var client = message.ReadShort();
					var modelIndex = message.ReadShort();
					var count = message.ReadByte();
					var size = message.ReadByte();

					PlayerSprites( client, modelIndex, count, size );
					break;
				}

			case TempEntity.ParticleBurst:
				{
					var position = message.ReadPosition();
					var radius = message.ReadShort();
					var color = message.ReadByte();
					var life = (float)(message.ReadByte() / 10.0);

					ParticleBurst( position, radius, color, life );
					break;
				}

			case TempEntity.FireField:
				{
					var position = message.ReadPosition();
					var radius = message.ReadShort();
					var modelIndex = message.ReadShort();
					var count = message.ReadByte();
					var flags = message.ReadByte();
					var life = (float)(message.ReadByte() / 10.0);

					FireField( position, radius, modelIndex, count, flags, life );
					break;
				}

			case TempEntity.PlayerAttachment:
				{
					var client = message.ReadByte();
					var heightOffset = message.ReadCoord();
					var modelIndex = message.ReadShort();
					var life = message.ReadShort() / 10f;

					AttachTentToPlayer( client, modelIndex, heightOffset, life );
					break;
				}

			case TempEntity.MultiGunshot:
				{
					var position = message.ReadPosition();
					var x = (float)(message.ReadCoord() / 10.0);
					var y = (float)(message.ReadCoord() / 10.0);
					var z = (float)(message.ReadCoord() / 10.0);
					var noiseX = (float)(message.ReadCoord() / 100.0);
					var noiseY = (float)(message.ReadCoord() / 100.0);
					var count = message.ReadByte();

					_multiGunshotDecal[0] = message.ReadByte();

					MultiGunshot( position, new Vector3( x, y, z ), new Vector3( noiseX, noiseY, 0f ), count, _multiGunshotDecal );
					break;
				}

			case TempEntity.UserTracer:
				{
					var position = message.ReadPosition();
					var velocity = message.ReadPosition();
					var life = (float)(message.ReadByte() / 10.0);
					var color = message.ReadByte();
					var length = (float)(message.ReadByte() / 10.0);

					UserTracerParticle( position, velocity, life, color, length, 0, null );
					break;
				}

			case TempEntity.BreakModel:
				{
					var position = message.ReadPosition();
					var size = message.ReadPosition();
					var velocity = message.ReadPosition();
					var random = message.ReadByte() * 10f;
					var model = message.ReadShort();
					var count = message.ReadByte();
					var life = message.ReadByte() * 0.1f;
					var flags = message.ReadByte();

					BreakModel( position, size, velocity, random, life, count, model, flags );
					break;
				}

			default:
				throw new InvalidOperationException( "CL_ParseTEnt: bad type" );
		}
	}
}
