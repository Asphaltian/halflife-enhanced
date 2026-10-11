using System;

namespace GoldSrc;

partial class Client
{
	public const int FireFieldAllFloat = 1;
	public const int FireFieldSomeFloat = 2;
	public const int FireFieldLoop = 4;
	public const int FireFieldAlpha = 8;
	public const int FireFieldPlanar = 16;
	public const int FireFieldAdditive = 32;

	public void TempSphereModel( Vector3 position, float speed, float life, int count, int modelIndex )
	{
		if ( modelIndex == 0 || GetModelByIndex( modelIndex ) is not { } model )
			return;

		var frameCount = ModelFrames( modelIndex );

		for ( var i = 0; i < count; i++ )
		{
			if ( AllocTempEntity( position, model ) is not { } temp )
				return;

			temp.Body = Server.RandomLong( 0, frameCount - 1 );
			temp.Flags |= Server.RandomLong( 0, 255 ) < 10 ? TempEntityFlags.SlowGravity : TempEntityFlags.Gravity;

			if ( Server.RandomLong( 0, 255 ) < 220 )
			{
				temp.Flags |= TempEntityFlags.Rotate;
				temp.AngularVelocity.x = Server.RandomFloat( -256f, -255f );
				temp.AngularVelocity.y = Server.RandomFloat( -256f, -255f );
				temp.AngularVelocity.z = Server.RandomFloat( -256f, -255f );
			}

			if ( Server.RandomLong( 0, 255 ) < 100 )
				temp.Flags |= TempEntityFlags.SmokeTrail;

			temp.Flags |= TempEntityFlags.CollideWorld | TempEntityFlags.Flicker;
			temp.RenderMode = RenderMode.Normal;
			temp.Effects = i & 31;

			var direction = RandomVector( -1f, 1f );

			MathLib.VectorNormalize( ref direction );

			temp.Velocity = direction * speed;
			temp.Die = (float)(life + ClientTime);
		}
	}

	public void Projectile( Vector3 origin, Vector3 velocity, int modelIndex, int life, int owner, Action<TempEnt, PmTrace> hitCallback )
	{
		if ( GetModelByIndex( modelIndex ) is not { } model )
			return;

		if ( AllocTempEntityHigh( origin, model ) is not { } temp )
		{
			Con.Printf( "Couldn't allocate temp ent in R_Projectile!\n" );
			return;
		}

		if ( owner < 0 || owner > MaxClients )
		{
			Con.Printf( "Bad ent in R_Projectile!\n" );
			return;
		}

		temp.Flags |= TempEntityFlags.CollideAll | TempEntityFlags.Persist | TempEntityFlags.CollideKill;
		temp.Velocity = velocity;
		temp.ClientIndex = (short)owner;

		if ( model.Type == ModelType.Sprite )
		{
			var frameCount = ModelFrames( modelIndex );

			temp.Flags |= TempEntityFlags.SprAnimate;
			temp.FrameMax = frameCount;

			if ( frameCount < 10 )
			{
				temp.Flags |= TempEntityFlags.SprAnimateLoop;
				temp.FrameRate = 10f;
			}
			else
			{
				temp.FrameRate = frameCount / life;
			}
		}
		else
		{
			temp.FrameMax = 0f;

			MathLib.VectorNormalize( ref velocity );

			temp.Angles = MathLib.VectorAngles( velocity );
		}

		temp.RenderMode = RenderMode.Normal;
		temp.RenderFx = RenderFx.None;
		temp.RenderAmt = 255f;
		temp.BaselineRenderAmt = 255f;
		temp.Frame = 0f;
		temp.Body = 0;
		temp.Scale = 1f;
		temp.RenderColor = new Vector3( 255f, 255f, 255f );
		temp.HitCallback = hitCallback;
		temp.Die = (float)(life + ClientTime);
	}

	public void Spray( Vector3 position, Vector3 direction, int modelIndex, int count, int speed, int noise, RenderMode renderMode )
	{
		var noiseScale = noise / 100f;
		var upNoise = (float)(noiseScale * 1.5) <= 1f ? (float)(noiseScale * 1.5) : 1f;

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Con.Printf( $"No model {modelIndex}!\n" );
			return;
		}

		var lastFrame = ModelFrames( modelIndex ) - 1;

		for ( var i = 0; i < count; i++ )
		{
			if ( AllocTempEntity( position, model ) is not { } temp )
				return;

			temp.RenderMode = renderMode;
			temp.RenderAmt = 255f;
			temp.BaselineRenderAmt = 255f;
			temp.RenderFx = RenderFx.NoDissipation;
			temp.FrameRate = 1f;
			temp.Flags |= TempEntityFlags.SlowGravity | TempEntityFlags.CollideWorld;

			if ( lastFrame < 2 )
			{
				temp.Die = (float)(ClientTime + 0.35);
			}
			else
			{
				temp.Flags |= TempEntityFlags.SprAnimate;
				temp.FrameRate = 10f;
				temp.Die = (float)((lastFrame * 0.1) + ClientTime);
			}

			temp.Velocity.x = Server.RandomFloat( -noiseScale, noiseScale ) + direction.x;
			temp.Velocity.y = Server.RandomFloat( -noiseScale, noiseScale ) + direction.y;
			temp.Velocity.z = Server.RandomFloat( 0f, upNoise ) + direction.z;
			temp.Velocity *= Server.RandomFloat( (float)(speed * 0.8), (float)(speed * 1.2) );

			temp.Origin = position;
			temp.Frame = 0f;
			temp.FrameMax = lastFrame;
		}
	}

	public void PlayerSprites( int client, int modelIndex, int count, int size )
	{
		var minFrameRate = 1.0 - (size / 100.0);

		if ( client <= 0 || client > MaxClients )
		{
			Con.Printf( "Bad ent in R_PlayerSprites!\n" );
			return;
		}

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Con.Printf( $"No model {modelIndex}!\n" );
			return;
		}

		if ( GetEntity( client ) is not { } player )
			return;

		var lastFrame = ModelFrames( modelIndex ) - 1;

		for ( var i = 0; i < count; i++ )
		{
			var x = Server.RandomFloat( -10f, 10f ) + player.Origin.x;
			var y = Server.RandomFloat( -10f, 10f ) + player.Origin.y;
			var z = Server.RandomFloat( -20f, 36f ) + player.Origin.z;
			var position = new Vector3( x, y, z );

			if ( AllocTempEntity( position, model ) is not { } temp )
				return;

			temp.TentOffset = temp.Origin - player.Origin;

			if ( i == 0 )
			{
				var direction = position - player.Origin;

				MathLib.VectorNormalize( ref direction );

				direction *= 60f;

				temp.Velocity.x = direction.x;
				temp.Velocity.y = direction.y;
				temp.Velocity.z = Server.RandomFloat( 20f, 60f );
			}
			else
			{
				temp.Flags |= TempEntityFlags.PlyrAttachment;
				temp.ClientIndex = (short)client;
			}

			temp.RenderMode = RenderMode.Normal;
			temp.RenderAmt = 255f;
			temp.BaselineRenderAmt = 255f;
			temp.RenderFx = RenderFx.NoDissipation;
			temp.FrameRate = Server.RandomFloat( (float)minFrameRate, 1f );

			if ( lastFrame < 2 )
			{
				temp.Die = (float)(ClientTime + 0.35);
			}
			else
			{
				temp.Flags |= TempEntityFlags.SprAnimate;
				temp.FrameRate = 20f;
				temp.Die = (float)((lastFrame * 0.05) + ClientTime);
			}

			temp.Frame = 0f;
			temp.FrameMax = lastFrame;
		}
	}

	public void FireField( Vector3 origin, int radius, int modelIndex, int count, int flags, float life )
	{
		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Con.Printf( $"No model {modelIndex}!\n" );
			return;
		}

		var lastFrame = ModelFrames( modelIndex ) - 1;

		for ( var i = 0; i < count; i++ )
		{
			var x = Server.RandomFloat( -radius, radius ) + origin.x;
			var y = Server.RandomFloat( -radius, radius ) + origin.y;
			var z = (flags & FireFieldPlanar) == 0 ? Server.RandomFloat( -radius, radius ) + origin.z : origin.z;

			if ( AllocTempEntity( new Vector3( x, y, z ), model ) is not { } temp )
				return;

			if ( (flags & FireFieldAlpha) != 0 )
			{
				temp.RenderMode = RenderMode.TransAlpha;
				temp.RenderAmt = 128f;
				temp.BaselineRenderAmt = 128f;
				temp.RenderFx = RenderFx.NoDissipation;
			}
			else if ( (flags & FireFieldAdditive) != 0 )
			{
				temp.RenderMode = RenderMode.TransAdd;
				temp.RenderAmt = 180f;
			}
			else
			{
				temp.RenderMode = RenderMode.Normal;
				temp.RenderAmt = 255f;
				temp.BaselineRenderAmt = 255f;
				temp.RenderFx = RenderFx.NoDissipation;
			}

			temp.FrameRate = Server.RandomFloat( 0.75f, 1.25f );

			var duration = Server.RandomFloat( -0.25f, 0.5f ) + life;

			temp.Die = (float)(duration + ClientTime);

			if ( lastFrame > 1 )
			{
				temp.Flags |= TempEntityFlags.SprAnimate;

				if ( (flags & FireFieldLoop) != 0 )
				{
					temp.FrameRate = 15f;
					temp.Flags |= TempEntityFlags.SprAnimateLoop;
				}
				else
				{
					temp.FrameRate = lastFrame / duration;
				}
			}

			temp.Frame = 0f;
			temp.Velocity = Vector3.Zero;
			temp.FrameMax = lastFrame;

			if ( (flags & FireFieldAllFloat) != 0 || ((flags & FireFieldSomeFloat) != 0 && Server.RandomLong( 0, 1 ) == 0) )
				temp.Velocity.z = Server.RandomFloat( 10f, 30f );
		}
	}

	public void AttachTentToPlayer( int client, int modelIndex, float heightOffset, float life )
	{
		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Con.Printf( $"No model {modelIndex}!\n" );
			return;
		}

		if ( client < 0 || client > MaxClients )
		{
			Con.Printf( "Bad client in R_AttachTentToPlayer()!\n" );
			return;
		}

		if ( GetCurrentEntity( client ) is not { } player )
			return;

		var position = new Vector3( player.Origin.x, player.Origin.y, heightOffset + player.Origin.z );

		if ( AllocTempEntityHigh( position, model ) is not { } temp )
		{
			Con.Printf( "No temp ent.\n" );
			return;
		}

		temp.FrameRate = 1f;
		temp.TentOffset = new Vector3( 0f, 0f, heightOffset );
		temp.RenderMode = RenderMode.Normal;
		temp.RenderAmt = 255f;
		temp.BaselineRenderAmt = 255f;
		temp.RenderFx = RenderFx.NoDissipation;
		temp.ClientIndex = (short)client;
		temp.Flags |= TempEntityFlags.PlyrAttachment | TempEntityFlags.Persist;
		temp.Die = (float)(life + ClientTime);

		if ( model.Type == ModelType.Sprite )
		{
			temp.FrameRate = 10f;
			temp.Flags |= TempEntityFlags.SprAnimate | TempEntityFlags.SprAnimateLoop;
			temp.FrameMax = ModelFrames( modelIndex );
		}
		else
		{
			temp.FrameMax = 0f;
		}

		temp.Frame = 0f;
	}

	public void MultiGunshot( Vector3 origin, Vector3 direction, Vector3 noise, int count, int[] decalIndices )
	{
		MathLib.AngleVectors( MathLib.VectorAngles( direction ), out _, out var right, out var up );

		for ( var i = 0; i < count; i++ )
		{
			float x;
			float y;

			do
			{
				x = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
				y = Server.RandomFloat( -0.5f, 0.5f ) + Server.RandomFloat( -0.5f, 0.5f );
			}
			while ( (y * y) + (x * x) > 1f );

			var decal = decalIndices[Server.RandomLong( 0, decalIndices.Length - 1 )];
			var shot = (right * (x * noise.x)) + (up * (y * noise.y)) + direction;
			var end = (shot * 4096f) + origin;

			_pmove.UseHull = 2;

			var trace = _pmove.PlayerTrace( origin, end, TraceFlags.StudioIgnore, -1 );

			if ( trace.Fraction == 1f )
				continue;

			if ( (i & 2) != 0 )
				RicochetSound( trace.EndPos );

			BulletImpactParticles( trace.EndPos );

			if ( DecalLimit != 0f )
				DecalShoot( decal, _pmove.PhysEnts[trace.Ent].Info, 0, trace.EndPos, default );
		}
	}
}
