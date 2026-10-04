using System;

namespace GoldSrc;

partial class Client
{
	private static readonly string[] RicochetSoundNames = ["weapons/ric1.wav", "weapons/ric2.wav", "weapons/ric3.wav", "weapons/ric4.wav", "weapons/ric5.wav"];

	private Model _ricochetSprite;
	private int _trailCount;

	private Particle NextParticle()
	{
		if ( _freeParticles is not { } particle )
			return null;

		_freeParticles = particle.Next;
		particle.Next = _activeParticles;
		_activeParticles = particle;

		return particle;
	}

	private Particle NextTracer()
	{
		if ( _freeParticles is not { } particle )
			return null;

		_freeParticles = particle.Next;
		particle.Next = _activeTracers;
		_activeTracers = particle;

		return particle;
	}

	private static Vector3 RandomVector( float low, float high )
	{
		var x = Server.RandomFloat( low, high );
		var y = Server.RandomFloat( low, high );
		var z = Server.RandomFloat( low, high );

		return new Vector3( x, y, z );
	}

	public TempEnt AllocTempEntityHigh( Vector3 origin, Model model )
	{
		if ( model is null )
		{
			Log.Trace( "temporary ent model invalid" );
			return null;
		}

		TempEnt temp;

		if ( _freeTempEntities is not null )
		{
			temp = _freeTempEntities;
			_freeTempEntities = temp.Next;
			temp.Next = _activeTempEntities;
			_activeTempEntities = temp;
		}
		else
		{
			temp = _activeTempEntities;

			while ( temp is not null && temp.Priority != 0 )
				temp = temp.Next;

			if ( temp is null )
			{
				Log.Trace( "Couldn't alloc a high priority TENT!" );
				return null;
			}
		}

		temp.Prepare( model, ClientTime );
		temp.Priority = 1;
		temp.Origin = origin;

		return temp;
	}

	public void RunParticleEffect( Vector3 origin, Vector3 direction, int color, int count )
	{
		for ( var i = 0; i < count; i++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			if ( count == 1024 )
			{
				particle.Die = (float)(ClientTime + 5.0);
				particle.Color = Ramp1[0];
				particle.PackedColor = 0;
				particle.Ramp = Server.RandomLong( 0, 3 );
				particle.Type = (i & 1) != 0 ? ParticleType.Explode : ParticleType.Explode2;

				for ( var j = 0; j < 3; j++ )
				{
					particle.Org[j] = Server.RandomFloat( -16f, 16f ) + origin[j];
					particle.Vel[j] = Server.RandomFloat( -256f, 256f );
				}

				continue;
			}

			particle.Die = (float)(Server.RandomFloat( 0f, 0.4f ) + ClientTime);
			particle.Color = (short)(Server.RandomLong( 0, 7 ) + (color & 0xfff8));
			particle.PackedColor = 0;
			particle.Type = ParticleType.SlowGrav;

			for ( var j = 0; j < 3; j++ )
			{
				particle.Org[j] = Server.RandomFloat( -8f, 8f ) + origin[j];
				particle.Vel[j] = direction[j] * 15f;
			}
		}
	}

	public void ShowLine( Vector3 start, Vector3 end )
	{
		var direction = end - start;
		var length = MathLib.VectorNormalize( ref direction );

		direction *= 5f;

		while ( length > 0f )
		{
			length -= 5f;

			if ( NextParticle() is not { } particle )
				return;

			particle.Vel = Vector3.Zero;
			particle.Die = (float)(ClientTime + 30.0);
			particle.Color = 75;
			particle.PackedColor = 0;
			particle.Type = ParticleType.Static;
			particle.Org = start;

			start += direction;
		}
	}

	private void ParseParticle( MessageReader message )
	{
		var origin = message.ReadPosition();
		var direction = new Vector3( message.ReadChar(), message.ReadChar(), message.ReadChar() ) * 0.0625f;
		var count = message.ReadByte();
		var color = message.ReadByte();

		RunParticleEffect( origin, direction, color, count == 255 ? 1024 : count );
	}

	public void RicochetSound( Vector3 position )
	{
		var sound = RicochetSoundNames[Server.RandomLong( 0, 4 )];

		Sound.StartDynamicSound( -1, 0, Sound.PrecacheSound( sound ), position, 1f, 1f, 0, 100 );
	}

	public void SparkStreaks( Vector3 position, int count, int velocityMin, int velocityMax )
	{
		for ( var i = 0; i < count; i++ )
		{
			if ( NextTracer() is not { } tracer )
				return;

			tracer.Die = (float)(Server.RandomFloat( 0.1f, 0.5f ) + ClientTime);
			tracer.Color = 5;
			tracer.PackedColor = 255;
			tracer.Type = ParticleType.Grav;
			tracer.Ramp = 0.5f;
			tracer.Org = position;
			tracer.Vel = RandomVector( velocityMin, velocityMax );
		}
	}

	public void StreakSplash( Vector3 position, Vector3 direction, int color, int count, float speed, int velocityMin, int velocityMax )
	{
		var initialVelocity = direction * speed;

		for ( var i = 0; i < count; i++ )
		{
			if ( NextTracer() is not { } tracer )
				return;

			tracer.Die = (float)(Server.RandomFloat( 0.1f, 0.5f ) + ClientTime);
			tracer.Color = (short)color;
			tracer.PackedColor = 255;
			tracer.Type = ParticleType.Grav;
			tracer.Ramp = 1f;
			tracer.Org = position;
			tracer.Vel = RandomVector( velocityMin, velocityMax ) + initialVelocity;
		}
	}

	private const float EntityParticleDistance = 64f;
	private const float EntityParticleBeamLength = 16f;

	private static float[] _angularVelocities = new float[VertexNormalCount * 3];

	private Model _rocketFlareSprite;

	private const int ModelFlagRocket = 1;
	private const int ModelFlagGrenade = 2;
	private const int ModelFlagGib = 4;
	private const int ModelFlagTracer = 16;
	private const int ModelFlagZomGib = 32;
	private const int ModelFlagTracer2 = 64;
	private const int ModelFlagTracer3 = 128;

	private void LinkEntityTrail( ClientEntity entity, int modelFlags, Vector3 origin )
	{
		var oldOrigin = entity.TrailOrigin;

		if ( (modelFlags & ModelFlagGib) != 0 )
		{
			RocketTrail( oldOrigin, origin, 2 );
		}
		else if ( (modelFlags & ModelFlagZomGib) != 0 )
		{
			RocketTrail( oldOrigin, origin, 4 );
		}
		else if ( (modelFlags & ModelFlagTracer) != 0 )
		{
			RocketTrail( oldOrigin, origin, 3 );
		}
		else if ( (modelFlags & ModelFlagTracer2) != 0 )
		{
			RocketTrail( oldOrigin, origin, 5 );
		}
		else if ( (modelFlags & ModelFlagRocket) != 0 )
		{
			RocketTrail( oldOrigin, origin, 0 );

			var light = AllocDlight( entity.Index );

			light.Origin = origin;
			light.Radius = 200f;
			light.R = 200;
			light.G = 200;
			light.B = 200;
			light.Die = (float)(ClientTime + 0.01);
		}
		else if ( (modelFlags & ModelFlagGrenade) != 0 )
		{
			RocketTrail( oldOrigin, origin, 1 );
		}
		else if ( (modelFlags & ModelFlagTracer3) != 0 )
		{
			RocketTrail( oldOrigin, origin, 6 );
		}
	}

	private void LinkEntityEffects( ClientEntity entity, Vector3 origin )
	{
		var effects = (Effects)entity.CurState.Effects;

		if ( (effects & Effects.BrightField) == Effects.BrightField )
			EntityParticles( origin );

		if ( entity.Index != 1 )
		{
			if ( (effects & Effects.BrightLight) == Effects.BrightLight )
			{
				var light = AllocDlight( entity.Index );

				light.Origin = origin + new Vector3( 0f, 0f, 16f );
				light.R = 250;
				light.G = 250;
				light.B = 250;
				light.Radius = Server.RandomFloat( 400f, 431f );
				light.Die = (float)(ClientTime + 0.001);
			}

			if ( (effects & Effects.DimLight) == Effects.DimLight )
			{
				var light = AllocDlight( entity.Index );

				light.Origin = origin;
				light.R = 100;
				light.G = 100;
				light.B = 100;
				light.Radius = Server.RandomFloat( 200f, 231f );
				light.Die = (float)(ClientTime + 0.001);
			}
		}

		if ( (effects & Effects.Light) == Effects.Light )
		{
			var light = AllocDlight( entity.Index );

			light.Origin = origin;
			light.Radius = 200f;
			light.R = 100;
			light.G = 100;
			light.B = 100;
			light.Die = (float)(ClientTime + 0.001);

			RocketFlare( origin );
		}
	}

	public void EntityParticles( Vector3 origin )
	{
		if ( _angularVelocities[0] == 0f )
		{
			for ( var i = 0; i < _angularVelocities.Length; i++ )
				_angularVelocities[i] = Server.RandomFloat( 0f, 2.55f );
		}

		for ( var i = 0; i < VertexNormalCount; i++ )
		{
			var yaw = (float)(_angularVelocities[i * 3] * ClientTime);
			var pitch = (float)(_angularVelocities[(i * 3) + 1] * ClientTime);

			var sy = (float)Math.Sin( yaw );
			var cy = (float)Math.Cos( yaw );
			var sp = (float)Math.Sin( pitch );
			var cp = (float)Math.Cos( pitch );

			if ( AllocParticle( null ) is not { } particle )
				return;

			particle.Die = (float)(ClientTime + 0.01);
			particle.Color = 111;
			particle.Type = ParticleType.Explode;
			particle.Org = (VertexNormals[i] * EntityParticleDistance) + origin + (new Vector3( cp * cy, cp * sy, -sp ) * EntityParticleBeamLength);
		}
	}

	public void RocketFlare( Vector3 position )
	{
		if ( ClientTime == _oldTime )
			return;

		_rocketFlareSprite ??= ModelCache.ForName( "sprites/animglow01.spr" );

		var frameCount = Server.ModelFrames( _rocketFlareSprite );

		if ( AllocTempEntity( position, _rocketFlareSprite ) is not { } temp )
			return;

		temp.RenderMode = RenderMode.Glow;
		temp.RenderAmt = 255f;
		temp.RenderFx = RenderFx.NoDissipation;
		temp.FrameRate = 1f;
		temp.FrameMax = frameCount;
		temp.Origin = position;
		temp.Die = (float)(ClientTime + 0.01);
		temp.Frame = Server.RandomLong( 0, frameCount - 1 );
	}

	public void RicochetSprite( Vector3 position, Model model, float duration, float scale )
	{
		if ( AllocTempEntity( position, model ) is not { } temp )
			return;

		temp.Scale = scale;
		temp.RenderMode = RenderMode.Glow;
		temp.RenderFx = RenderFx.NoDissipation;
		temp.RenderAmt = 200f;
		temp.BaselineRenderAmt = 200f;
		temp.Flags = TempEntityFlags.FadeOut;
		temp.Velocity = Vector3.Zero;
		temp.Origin = position;
		temp.FadeSpeed = 8f;
		temp.Die = (float)ClientTime;
		temp.Frame = 0f;
		temp.Angles.z = 45 * Server.RandomLong( 0, 7 );
	}

	public void SparkShower( Vector3 position )
	{
		if ( AllocTempEntityNoModel( position ) is not { } temp )
			return;

		var x = Server.RandomFloat( -300f, 300f );
		var y = Server.RandomFloat( -300f, 300f );
		var z = Server.RandomFloat( -200f, 200f );

		temp.Velocity = new Vector3( x, y, z );
		temp.Flags |= TempEntityFlags.SlowGravity | TempEntityFlags.CollideWorld | TempEntityFlags.SparkShower;
		temp.AngularVelocity = Vector3.Zero;
		temp.Die = (float)(ClientTime + 0.5);
		temp.BaselineFrameRate = Server.RandomFloat( 0.5f, 1.5f );
		temp.BaselineScale = (float)ClientTime;
	}

	public void SparkEffect( Vector3 position, int count, int velocityMin, int velocityMax )
	{
		SparkStreaks( position, count, velocityMin, velocityMax );

		_ricochetSprite ??= ModelCache.ForName( "sprites/richo1.spr" );

		RicochetSprite( position, _ricochetSprite, 0.1f, Server.RandomFloat( 0.5f, 1f ) );
	}

	public void Blood( Vector3 origin, Vector3 direction, int color, int speed )
	{
		var pitch = speed * 3;

		MathLib.VectorNormalize( ref direction );

		for ( var count = 0; count < speed / 2; count++ )
		{
			var spot = origin + RandomVector( -3f, 3f );
			var heading = direction + RandomVector( -0.06f, 0.06f );

			for ( var i = 0; i < 8; i++ )
			{
				if ( NextParticle() is not { } particle )
					return;

				particle.Die = (float)(ClientTime + 1.5);
				particle.Color = (short)(Server.RandomLong( 0, 9 ) + color);
				particle.PackedColor = 0;
				particle.Type = ParticleType.VoxGrav;
				particle.Org = spot + RandomVector( -1f, 1f );
				particle.Vel = heading * pitch;
			}

			pitch -= speed;
		}
	}

	public void BloodStream( Vector3 origin, Vector3 direction, int color, int speed )
	{
		var arc = 0.05f;
		var streamSpeed = speed;

		MathLib.VectorNormalize( ref direction );

		for ( var count = 0; count < 100; count++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Die = (float)(ClientTime + 2.0);
			particle.Color = (short)(Server.RandomLong( 0, 9 ) + color);
			particle.PackedColor = 0;
			particle.Type = ParticleType.VoxGrav;
			particle.Org = origin;

			var heading = direction;

			heading.z -= arc;
			arc -= 0.005f;

			particle.Vel = heading * streamSpeed;

			streamSpeed = (int)(streamSpeed - 0.00001);
		}

		arc = 0.075f;

		for ( var count = 0; count < speed / 5; count++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Die = (float)(ClientTime + 3.0);
			particle.Color = (short)(Server.RandomLong( 0, 9 ) + color);
			particle.PackedColor = 0;
			particle.Type = ParticleType.VoxSlowGrav;
			particle.Org = origin;

			var heading = direction;

			heading.z -= arc;
			arc -= 0.005f;

			var fraction = Server.RandomFloat( 0f, 1f );
			var dropSpeed = (int)(speed * fraction);

			fraction *= 1.7f;
			heading *= fraction;

			particle.Vel = heading * dropSpeed;

			for ( var i = 0; i < 2; i++ )
			{
				if ( NextParticle() is not { } drop )
					return;

				drop.Die = (float)(ClientTime + 3.0);
				drop.Color = (short)(Server.RandomLong( 0, 9 ) + color);
				drop.PackedColor = 0;
				drop.Type = ParticleType.VoxSlowGrav;
				drop.Org = origin + RandomVector( -1f, 1f );

				heading = direction;
				heading.z -= arc;
				heading *= fraction;

				drop.Vel = heading * dropSpeed;
			}
		}
	}

	private Vector3 PaletteColor( int index )
	{
		_palette ??= Server.LoadMountFile( "gfx/palette.lmp" );

		return new Vector3( _palette[index * 3], _palette[(index * 3) + 1], _palette[(index * 3) + 2] );
	}

	public void BloodSprite( Vector3 origin, int colorIndex, int modelIndex, int modelIndex2, float size )
	{
		colorIndex += Server.RandomLong( 1, 3 );

		var sprayColor = PaletteColor( colorIndex );
		var dropColor = PaletteColor( colorIndex - 1 );

		if ( modelIndex2 != 0 && GetModelByIndex( modelIndex2 ) is { } dropModel )
		{
			var frameCount = ModelFrames( modelIndex2 );
			var count = (int)(Server.RandomLong( 1, 8 ) + Server.RandomLong( 1, 8 ) + size);

			for ( var i = 0; i < count; i++ )
			{
				if ( AllocTempEntity( origin, dropModel ) is not { } temp )
					continue;

				temp.RenderMode = RenderMode.Normal;
				temp.RenderFx = RenderFx.ClampMinScale;
				temp.BaselineRenderAmt = 250f;
				temp.RenderAmt = 250f;
				temp.Scale = Server.RandomFloat( size / 15f, size / 25f );
				temp.RenderColor = dropColor;
				temp.Flags = TempEntityFlags.CollideWorld | TempEntityFlags.SlowGravity | TempEntityFlags.Rotate;

				temp.Velocity.x = Server.RandomFloat( -96f, 95f );
				temp.Velocity.y = Server.RandomFloat( -96f, 95f );
				temp.Velocity.z = Server.RandomFloat( -32f, 95f );
				temp.AngularVelocity = RandomVector( -256f, -255f );
				temp.FrameRate = 0f;
				temp.Die = (float)(Server.RandomFloat( 1f, 2f ) + ClientTime);
				temp.Frame = Server.RandomLong( 1, frameCount - 1 );

				if ( temp.Frame > 8f )
					temp.Frame = frameCount - 1;

				temp.Angles.z = Server.RandomLong( 0, 360 );
				temp.BounceFactor = 0f;
			}
		}

		if ( modelIndex != 0 && GetModelByIndex( modelIndex ) is { } sprayModel )
		{
			var frameCount = ModelFrames( modelIndex );

			if ( AllocTempEntityHigh( origin, sprayModel ) is not { } temp )
				return;

			temp.RenderMode = RenderMode.Normal;
			temp.RenderFx = RenderFx.ClampMinScale;
			temp.BaselineRenderAmt = 250f;
			temp.RenderAmt = 250f;
			temp.Scale = Server.RandomFloat( size / 25f, size / 35f );
			temp.RenderColor = sprayColor;
			temp.Flags = TempEntityFlags.SprAnimate;
			temp.Velocity = Vector3.Zero;
			temp.FrameRate = frameCount * 4;
			temp.Die = (float)(((float)frameCount / (frameCount * 4)) + ClientTime);
			temp.Frame = 0f;
			temp.FrameMax = frameCount;
			temp.Angles.z = Server.RandomLong( 0, 360 );
			temp.BounceFactor = 0f;
		}
	}

	public TempEnt TempSprite( Vector3 position, Vector3 direction, float scale, int modelIndex, RenderMode renderMode, RenderFx renderFx, float alpha, float life, TempEntityFlags flags )
	{
		if ( modelIndex == 0 )
			return null;

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Log.Info( $"No model {modelIndex}!" );
			return null;
		}

		var frameCount = ModelFrames( modelIndex );

		if ( AllocTempEntity( position, model ) is not { } temp )
			return null;

		temp.RenderMode = renderMode;
		temp.RenderFx = renderFx;
		temp.FrameRate = 10f;
		temp.FrameMax = frameCount;
		temp.RenderColor = new Vector3( 255f, 255f, 255f );
		temp.RenderAmt = (int)(alpha * 255f);
		temp.BaselineRenderAmt = (int)(alpha * 255f);
		temp.Flags |= flags;
		temp.Scale = scale;
		temp.Velocity = direction;
		temp.Origin = position;
		temp.Die = life == 0f ? (float)((frameCount * 0.1) + ClientTime + 1.0) : (float)(life + ClientTime);
		temp.Frame = 0f;

		return temp;
	}

	public void SpriteTrail( int type, Vector3 start, Vector3 end, int modelIndex, int count, float life, float size, float amplitude, int renderAmt, float speed )
	{
		if ( GetModelByIndex( modelIndex ) is not { } model )
			return;

		var frameCount = ModelFrames( modelIndex );
		var delta = end - start;
		var direction = delta;

		MathLib.VectorNormalize( ref direction );

		amplitude *= 0.00390625f;

		for ( var i = 0; i < count; i++ )
		{
			var fraction = i == 0 ? 0f : i / (count - 1f);

			if ( AllocTempEntity( start + (delta * fraction), model ) is not { } temp )
				return;

			temp.Flags |= TempEntityFlags.SprCycle | TempEntityFlags.FadeOut | TempEntityFlags.CollideWorld | TempEntityFlags.SlowGravity;

			temp.Velocity = direction * speed;
			temp.Velocity.x += Server.RandomLong( -127, 128 ) * amplitude;
			temp.Velocity.y += Server.RandomLong( -127, 128 ) * amplitude;
			temp.Velocity.z += Server.RandomLong( -127, 128 ) * amplitude;

			temp.RenderMode = RenderMode.Glow;
			temp.RenderFx = RenderFx.NoDissipation;
			temp.RenderAmt = renderAmt;
			temp.Scale = size;
			temp.Die = (float)(life + ClientTime + Server.RandomFloat( 0f, 4f ));
			temp.Frame = Server.RandomLong( 0, frameCount - 1 );
			temp.BaselineRenderAmt = renderAmt;
			temp.RenderColor = new Vector3( 255f, 255f, 255f );
			temp.FrameMax = frameCount;
		}
	}

	public void SpriteSpray( Vector3 position, Vector3 direction, int modelIndex, int count, int speed, int noise )
	{
		var noiseScale = noise / 100f;
		var upNoise = noiseScale * 1.5f <= 1f ? noiseScale * 1.5f : 1f;

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Log.Info( $"No model {modelIndex}!" );
			return;
		}

		var frameCount = ModelFrames( modelIndex );

		for ( var i = 0; i < count; i++ )
		{
			if ( AllocTempEntity( position, model ) is not { } temp )
				return;

			temp.Flags |= TempEntityFlags.SlowGravity | TempEntityFlags.FadeOut;
			temp.RenderMode = RenderMode.TransAlpha;
			temp.RenderAmt = 255f;
			temp.BaselineRenderAmt = 255f;
			temp.RenderFx = RenderFx.NoDissipation;
			temp.FrameRate = 0.5f;
			temp.FadeSpeed = 2f;

			temp.Velocity.x = Server.RandomFloat( -noiseScale, noiseScale ) + direction.x;
			temp.Velocity.y = Server.RandomFloat( -noiseScale, noiseScale ) + direction.y;
			temp.Velocity.z = Server.RandomFloat( 0f, upNoise ) + direction.z;
			temp.Velocity *= Server.RandomFloat( speed * 0.8f, speed * 1.2f );

			temp.Origin = position;
			temp.Die = (float)(ClientTime + 0.35);
			temp.Frame = Server.RandomLong( 0, frameCount - 1 );
		}
	}

	private void Bubble( TempEnt temp, Vector3 origin, float bottom, float height, int frameCount, float speed )
	{
		temp.Flags |= TempEntityFlags.SineWave;
		temp.X = origin.x;
		temp.Y = origin.y;

		double angle = Server.RandomLong( -3, 3 );

		temp.Velocity.x = (float)(Math.Cos( angle ) * speed);
		temp.Velocity.y = (float)(Math.Sin( angle ) * speed);

		float rise = Server.RandomLong( 80, 140 );

		temp.Velocity.z = rise;
		temp.Die = (float)(((height - (origin.z - bottom)) / rise) + ClientTime - 0.1);
		temp.Frame = Server.RandomLong( 0, frameCount - 1 );
		temp.Scale = 1f / Server.RandomFloat( 2f, 5f );
		temp.RenderMode = RenderMode.TransAlpha;
		temp.RenderAmt = 255f;
	}

	public void Bubbles( Vector3 mins, Vector3 maxs, float height, int modelIndex, int count, float speed )
	{
		if ( modelIndex == 0 || GetModelByIndex( modelIndex ) is not { } model )
			return;

		var frameCount = ModelFrames( modelIndex );

		for ( var i = 0; i < count; i++ )
		{
			float x = Server.RandomLong( (int)mins.x, (int)maxs.x );
			float y = Server.RandomLong( (int)mins.y, (int)maxs.y );
			float z = Server.RandomLong( (int)mins.z, (int)maxs.z );
			var origin = new Vector3( x, y, z );

			if ( AllocTempEntity( origin, model ) is not { } temp )
				return;

			Bubble( temp, origin, mins.z, height, frameCount, speed );
		}
	}

	public void BubbleTrail( Vector3 start, Vector3 end, float height, int modelIndex, int count, float speed )
	{
		if ( modelIndex == 0 || GetModelByIndex( modelIndex ) is not { } model )
			return;

		var frameCount = ModelFrames( modelIndex );

		for ( var i = 0; i < count; i++ )
		{
			var distance = Server.RandomFloat( 0f, 1f );
			var origin = ((end - start) * distance) + start;

			if ( AllocTempEntity( origin, model ) is not { } temp )
				return;

			Bubble( temp, origin, start.z, height, frameCount, speed );
		}
	}

	public void KillAttachedTents( int client )
	{
		if ( client < 0 || client > MaxClients )
		{
			Log.Info( "Bad client in R_KillAttachedTents()!" );
			return;
		}

		for ( var temp = _activeTempEntities; temp is not null; temp = temp.Next )
		{
			if ( (temp.Flags & TempEntityFlags.PlyrAttachment) == TempEntityFlags.PlyrAttachment && temp.ClientIndex == client )
				temp.Die = (float)ClientTime;
		}
	}

	private static readonly string[] ExplosionSoundNames = ["weapons/explode3.wav", "weapons/explode4.wav", "weapons/explode5.wav"];

	public TempEnt DefaultSprite( Vector3 position, int spriteIndex, float frameRate )
	{
		if ( ClientTime == _oldTime )
			return null;

		if ( spriteIndex == 0 || GetSpriteModel( spriteIndex ) is not { } sprite )
		{
			Log.Trace( $"No Sprite {spriteIndex}!" );
			return null;
		}

		var frameCount = ModelFrames( spriteIndex );

		if ( AllocTempEntity( position, sprite ) is not { } temp )
			return null;

		if ( frameRate == 0f )
			frameRate = 10f;

		temp.Flags |= TempEntityFlags.SprAnimate;
		temp.FrameMax = frameCount;
		temp.FrameRate = frameRate;
		temp.Die = (float)((frameCount / frameRate) + ClientTime);
		temp.Frame = 0f;
		temp.Origin = position;

		return temp;
	}

	public void SpriteExplode( TempEnt temp, float scale, int flags )
	{
		if ( temp is null )
			return;

		var additive = (flags & TempEntity.ExplosionNoAdditive) == 0;

		temp.RenderMode = additive ? RenderMode.TransAdd : RenderMode.Normal;
		temp.RenderAmt = additive ? 180f : 255f;
		temp.RenderFx = RenderFx.None;
		temp.Velocity.z = 8f;
		temp.RenderColor = Vector3.Zero;
		temp.Origin.z += 10f;
		temp.Scale = scale;
	}

	public void SpriteSmoke( TempEnt temp, float scale )
	{
		if ( temp is null )
			return;

		var color = Server.RandomLong( 20, 35 );

		temp.RenderMode = RenderMode.TransAlpha;
		temp.RenderAmt = 255f;
		temp.RenderFx = RenderFx.None;
		temp.Velocity.z = 30f;
		temp.RenderColor = new Vector3( color, color, color );
		temp.Origin.z += 20f;
		temp.Scale = scale;
	}

	public void Implosion( Vector3 end, float radius, int count, float life )
	{
		radius /= 100f;

		for ( var i = 0; i < count; i++ )
		{
			var offset = new Vector3(
				Server.RandomFloat( -100f, 100f ) * radius,
				Server.RandomFloat( -100f, 100f ) * radius,
				Server.RandomFloat( 0f, 100f ) * radius );

			if ( NextTracer() is not { } tracer )
				continue;

			tracer.Die = (float)(life + ClientTime);
			tracer.Color = 0;
			tracer.PackedColor = 255;
			tracer.Type = ParticleType.Static;
			tracer.Ramp = TracerLength;
			tracer.Org = offset + end;
			tracer.Vel = offset * (-1f / life);
		}
	}

	public void LargeFunnel( Vector3 origin, int reverse )
	{
		for ( var i = -256; i <= 256; i += 32 )
		{
			for ( var j = -256; j <= 256; j += 32 )
			{
				if ( NextParticle() is not { } particle )
					return;

				Vector3 direction;
				float height;

				if ( reverse == 0 )
				{
					particle.Org = new Vector3( origin.x + i, origin.y + j, Server.RandomFloat( 100f, 800f ) + origin.z );
					direction = origin - particle.Org;
					height = particle.Org.z;
				}
				else
				{
					particle.Org = origin;
					height = Server.RandomFloat( 100f, 800f ) + origin.z;
					direction = new Vector3( i + origin.x, j + origin.y, height ) - particle.Org;
				}

				particle.Color = 244;
				particle.PackedColor = 0;
				particle.Type = ParticleType.Static;

				var distance = MathLib.VectorNormalize( ref direction );
				var speed = Server.RandomFloat( 64f, 128f ) + MathF.Max( 64f, height * 0.125f );

				particle.Vel = direction * speed;
				particle.Die = (float)((distance / speed) + ClientTime);
			}
		}
	}

	public void FunnelSprite( Vector3 origin, int modelIndex, int reverse )
	{
		if ( modelIndex == 0 )
		{
			Log.Trace( "No modelindex for funnel!!" );
			return;
		}

		if ( GetModelByIndex( modelIndex ) is not { } model )
		{
			Log.Trace( $"No model {modelIndex}!" );
			return;
		}

		var frameCount = ModelFrames( modelIndex );

		for ( var i = -256; i <= 256; i += 64 )
		{
			for ( var j = -256; j <= 256; j += 64 )
			{
				if ( AllocTempEntity( origin, model ) is not { } temp )
					return;

				Vector3 direction;
				float height;

				if ( reverse == 0 )
				{
					temp.Origin = new Vector3( origin.x + i, origin.y + j, Server.RandomFloat( 100f, 800f ) + origin.z );
					direction = origin - temp.Origin;
					height = temp.Origin.z;
				}
				else
				{
					temp.Origin = origin;
					height = Server.RandomFloat( 100f, 800f ) + origin.z;
					direction = new Vector3( i + origin.x, j + origin.y, height ) - temp.Origin;
				}

				temp.RenderMode = RenderMode.Glow;
				temp.RenderFx = RenderFx.NoDissipation;
				temp.RenderAmt = 200f;
				temp.FrameMax = frameCount;
				temp.BaselineRenderAmt = 200f;
				temp.Flags = TempEntityFlags.FadeOut | TempEntityFlags.Rotate;
				temp.FrameRate = Server.RandomFloat( 0.1f, 0.4f );

				var distance = MathLib.VectorNormalize( ref direction );
				var speed = Server.RandomFloat( 64f, 128f ) + MathF.Max( 64f, height * 0.125f );

				temp.Velocity = direction * speed;
				temp.FadeSpeed = 2f;
				temp.Die = (float)((distance / speed) - 0.5 + ClientTime);
			}
		}
	}

	public void FizzEffect( ClientEntity entity, int modelIndex, int density )
	{
		if ( entity is null || GetModelByIndex( entity.ModelIndex ) is not { } volume || modelIndex == 0 )
			return;

		if ( GetModelByIndex( modelIndex ) is not { } model )
			return;

		var width = volume.Maxs.x - volume.Mins.x;
		var depth = volume.Maxs.y - volume.Mins.y;
		var height = volume.Maxs.z - volume.Mins.z;

		var speed = (entity.CurState.RenderColorR * 256f) + entity.CurState.RenderColorG;

		if ( entity.CurState.RenderColorB != 0 )
			speed = -speed;

		var yaw = entity.Angles.y * Math.PI / 180.0;
		var speedX = (float)(Math.Cos( yaw ) * speed);
		var speedY = (float)(Math.Sin( yaw ) * speed);

		var frameCount = ModelFrames( modelIndex );

		for ( var i = 0; i < density + 1; i++ )
		{
			var origin = new Vector3(
				Server.RandomLong( 0, (int)width - 1 ) + volume.Mins.x,
				Server.RandomLong( 0, (int)depth - 1 ) + volume.Mins.y,
				volume.Mins.z );

			if ( AllocTempEntity( origin, model ) is not { } temp )
				return;

			temp.Flags |= TempEntityFlags.SineWave;
			temp.X = origin.x;
			temp.Y = origin.y;

			float speedZ = Server.RandomLong( 80, 140 );

			temp.Velocity = new Vector3( speedX, speedY, speedZ );
			temp.Die = (float)((height / speedZ) + ClientTime - 0.1);
			temp.Frame = Server.RandomLong( 0, frameCount - 1 );
			temp.Scale = 1f / Server.RandomFloat( 2f, 5f );
			temp.RenderMode = RenderMode.TransAlpha;
			temp.RenderAmt = 255f;
		}
	}

	public void FlickerParticles( Vector3 origin )
	{
		for ( var i = 0; i < 15; i++ )
		{
			if ( NextParticle() is not { } particle )
				return;

			particle.Org = origin;
			particle.Vel = RandomVector( -32f, 32f );
			particle.Vel.z = Server.RandomFloat( 80f, 143f );
			particle.Ramp = 0f;
			particle.Color = 254;
			particle.PackedColor = 0;
			particle.Type = ParticleType.Blob2;
			particle.Die = (float)(ClientTime + 2.0);
		}
	}

	private void Explosion( Vector3 position, int modelIndex, float scale, float frameRate, int flags )
	{
		if ( scale != 0f )
		{
			SpriteExplode( DefaultSprite( position, modelIndex, frameRate ), scale, flags );

			if ( (flags & TempEntity.ExplosionNoParticles) == 0 )
				FlickerParticles( position );

			if ( (flags & TempEntity.ExplosionNoDLights) == 0 )
			{
				var flash = AllocDlight( 0 );

				flash.Origin = position;
				flash.Radius = 200f;
				flash.R = 250;
				flash.G = 250;
				flash.B = 150;
				flash.Die = (float)(ClientTime + 0.01);
				flash.Decay = 800f;

				var glow = AllocDlight( 0 );

				glow.Origin = position;
				glow.Radius = 150f;
				glow.R = 255;
				glow.G = 190;
				glow.B = 40;
				glow.Die = (float)(ClientTime + 1.0);
				glow.Decay = 200f;
			}
		}

		if ( (flags & TempEntity.ExplosionNoSound) == 0 )
			Sound.StartDynamicSound( -1, 0, Sound.PrecacheSound( ExplosionSoundNames[Server.RandomLong( 0, 2 )] ), position, 1f, 0.3f, 0, 100 );
	}

	public void RocketTrail( Vector3 start, Vector3 end, int type )
	{
		var direction = end - start;
		var length = MathLib.VectorNormalize( ref direction );
		var right = Vector3.Zero;
		var up = Vector3.Zero;
		int decrement;

		if ( type == 7 )
		{
			decrement = 1;
			MathLib.VectorMatrix( direction, out right, out up );
		}
		else if ( type < 128 )
		{
			decrement = 3;
		}
		else
		{
			decrement = 1;
			type -= 128;
		}

		direction *= decrement;

		while ( length > 0f )
		{
			length -= decrement;

			if ( NextParticle() is not { } particle )
				return;

			particle.Vel = Vector3.Zero;
			particle.Die = (float)(ClientTime + 2.0);

			switch ( type )
			{
				case 0:
				case 1:
				case 6:
					particle.Ramp = type == 1 ? Server.RandomLong( 2, 5 ) : Server.RandomLong( 0, 3 );
					particle.Color = Ramp3[(int)particle.Ramp];
					particle.Type = ParticleType.Fire;
					particle.Org = type == 6 ? start : start + RandomVector( -3f, 3f );
					break;

				case 2:
					particle.Type = ParticleType.Grav;
					particle.Color = (short)Server.RandomLong( 67, 74 );
					particle.Org = start + RandomVector( -3f, 3f );
					break;

				case 3:
				case 5:
					particle.Die = (float)(ClientTime + 0.5);
					particle.Type = ParticleType.Static;
					particle.Color = (short)((type == 3 ? 52 : 230) + ((_trailCount & 4) << 1));
					particle.Org = start;

					_trailCount++;

					if ( (_trailCount & 1) != 0 )
					{
						particle.Vel.x = direction.y * 30f;
						particle.Vel.y = -direction.x * 30f;
					}
					else
					{
						particle.Vel.x = -direction.y * 30f;
						particle.Vel.y = direction.x * 30f;
					}

					break;

				case 4:
					particle.Type = ParticleType.Grav;
					particle.Color = (short)Server.RandomLong( 67, 70 );
					particle.Org = start + RandomVector( -3f, 3f );
					length -= 3f;
					break;

				case 7:
				{
					double angle = Server.RandomLong( 0, 65535 );
					double radius = Server.RandomLong( 8, 16 );
					var sine = (float)(Math.Sin( angle ) * radius);
					var cosine = (float)(Math.Cos( angle ) * radius);

					particle.Org = start + (right * sine) + (up * cosine);
					particle.Vel = (start - particle.Org) * 2f;
					particle.Vel += direction * Server.RandomFloat( 96f, 111f );
					particle.Die = (float)(ClientTime + 2.0);
					particle.Type = ParticleType.Explode2;
					particle.Ramp = Server.RandomLong( 0, 3 );
					particle.Color = Ramp3[(int)particle.Ramp];
					break;
				}
			}

			particle.PackedColor = 0;
			start += direction;
		}
	}
}
