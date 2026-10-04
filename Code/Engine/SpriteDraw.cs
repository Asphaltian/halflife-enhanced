using System;

namespace GoldSrc;

public struct SpriteState
{
	public Vector3 Origin;
	public Vector3 Angles;
	public float Scale;
	public float Frame;
	public RenderMode RenderMode;
	public RenderFx RenderFx;
	public float RenderAmt;
	public Vector3 RenderColor;
}

public sealed class SpriteNextView
{
	private ModelRenderer _renderer;

	public void Draw( Client client, SpriteModel sprite, in SpriteState state, float blend )
	{
		if ( client.NextViewCamera is not { } camera )
		{
			Hide();
			return;
		}

		if ( !_renderer.IsValid() )
		{
			var gameObject = new GameObject( true, "sprite next view" );

			gameObject.NetworkMode = NetworkMode.Never;
			gameObject.Tags.Add( Client.NextViewTag );

			_renderer = gameObject.AddComponent<ModelRenderer>();
			_renderer.Model = client.SpriteQuad;
		}

		_renderer.Enabled = SpriteDraw.DrawModel( camera, sprite, _renderer, state, blend );
		_renderer.Attributes.SetCombo( "D_RENDER_MODE", (int)state.RenderMode );

		if ( _renderer.SceneObject is not { } sceneObject )
			return;

		sceneObject.Flags.IsTranslucent = true;
		sceneObject.Flags.IsOpaque = false;
		sceneObject.Flags.WantsPrePass = false;
	}

	public void Hide()
	{
		if ( _renderer.IsValid() )
			_renderer.Enabled = false;
	}

	public void Destroy()
	{
		if ( _renderer.IsValid() )
			_renderer.GameObject.Destroy();

		_renderer = null;
	}
}

public static class SpriteDraw
{
	private const float UprightLimit = 0.999848f;
	private const float GlowOccludeDistance = 8f;

	public static bool DrawModel( CameraComponent camera, SpriteModel sprite, ModelRenderer renderer, in SpriteState state, float blend )
	{
		var entityOrigin = state.Origin;
		var scale = state.Scale;

		if ( state.RenderMode == RenderMode.Glow )
		{
			var viewOrigin = camera.WorldPosition;
			var distance = MathLib.Length( entityOrigin - viewOrigin );
			var trace = Client.Current.TraceGlow( viewOrigin, entityOrigin );
			float brightness;

			if ( (1f - trace.Fraction) * distance > GlowOccludeDistance )
			{
				brightness = 0f;
			}
			else if ( state.RenderFx == RenderFx.NoDissipation )
			{
				brightness = state.RenderAmt * 0.003921569f;
			}
			else
			{
				brightness = MathF.Min( MathF.Max( 19000f / (distance * distance), 0.05f ), 1f );
				scale = distance * 0.005f;
			}

			blend *= brightness;
		}

		if ( blend == 0f )
			return false;

		var frameIndex = (int)state.Frame;

		if ( frameIndex >= sprite.Frames.Length || frameIndex < 0 )
			frameIndex = 0;

		if ( sprite.Frames[frameIndex] is not { } frame )
			return false;

		if ( scale <= 0f )
			scale = 1f;

		if ( state.RenderMode == RenderMode.Normal )
			blend = 1f;

		if ( !SpriteVectors( sprite.SpriteType, camera, entityOrigin, state.Angles, out var forward, out var up ) )
			return false;

		var right = Vector3.Cross( forward, up );
		var alpha = (int)(blend * 255f);
		var intensity = state.RenderMode is RenderMode.TransAdd or RenderMode.Glow ? alpha : 256;
		Vector3 color;

		if ( (byte)(int)state.RenderColor.x == 0 && (byte)(int)state.RenderColor.y == 0 && (byte)(int)state.RenderColor.z == 0 )
			color = new Vector3( (intensity * 255) >> 8 );
		else
			color = new Vector3( ((byte)(int)state.RenderColor.x * intensity) >> 8, ((byte)(int)state.RenderColor.y * intensity) >> 8, ((byte)(int)state.RenderColor.z * intensity) >> 8 );

		var colorAlpha = state.RenderMode is RenderMode.TransAdd or RenderMode.Glow ? 255 : alpha & 0xff;

		renderer.MaterialOverride = frame.Material;
		renderer.Attributes.Set( "SpriteColor", new Vector4( color / 255f, colorAlpha / 255f ) );

		renderer.WorldPosition = entityOrigin + (up * (scale * frame.Down)) + (right * (scale * frame.Left));
		renderer.WorldRotation = Rotation.LookAt( -forward, up );
		renderer.WorldScale = new Vector3( 1f, scale * (frame.Right - frame.Left), scale * (frame.Up - frame.Down) );

		return true;
	}

	private static bool SpriteVectors( SpriteType type, CameraComponent camera, Vector3 entityOrigin, Vector3 angles, out Vector3 forward, out Vector3 up )
	{
		var viewForward = camera.WorldRotation.Forward;
		var viewRight = camera.WorldRotation.Right;
		var viewUp = camera.WorldRotation.Up;

		forward = viewForward;
		up = viewUp;

		if ( angles.z != 0f && type == SpriteType.Parallel )
			type = SpriteType.ParallelOriented;

		switch ( type )
		{
			case SpriteType.ParallelUpright:
				if ( viewForward.z > UprightLimit || viewForward.z < -UprightLimit )
					return false;

				return UprightVectors( viewForward, out forward, out up );

			case SpriteType.FacingUpright:
				var direction = entityOrigin - camera.WorldPosition;
				MathLib.VectorNormalize( ref direction );

				if ( direction.z > UprightLimit || direction.z < -UprightLimit )
					return false;

				return UprightVectors( direction, out forward, out up );

			case SpriteType.Parallel:
				return true;

			case SpriteType.Oriented:
				MathLib.AngleVectors( angles, out forward, out _, out up );
				return true;

			case SpriteType.ParallelOriented:
				var angle = (float)(angles.z * 0.017453292519943295);
				var sr = (float)Math.Sin( angle );
				var cr = (float)Math.Cos( angle );

				up = (cr * viewUp) - (sr * viewRight);
				return true;

			default:
				throw new InvalidOperationException( $"R_DrawSprite: Bad sprite type {(int)type}" );
		}
	}

	private static bool UprightVectors( Vector3 direction, out Vector3 forward, out Vector3 up )
	{
		var right = new Vector3( direction.y, -direction.x, 0f );
		MathLib.VectorNormalize( ref right );

		forward = new Vector3( -right.y, right.x, 0f );
		up = new Vector3( 0f, 0f, 1f );

		return true;
	}
}
