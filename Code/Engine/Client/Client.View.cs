using System;

namespace GoldSrc;

partial class Client
{
	public const string FirstViewTag = "view0";
	public const string NextViewTag = "view1";

	private const string OnlyClientDrawTag = "onlyclientdraw";
	private const int ViewCount = 2;

	private CameraComponent _nextViewCamera;
	private int _drawView;
	private bool _nextViewWorld;
	private bool _firstViewOnlyClientDraw;
	private Color _firstViewBackground;

	public CameraComponent NextViewCamera => _nextViewWorld ? _nextViewCamera : null;

	private static string ViewTag( int view ) => view == 0 ? FirstViewTag : NextViewTag;

	private void SetupViewCamera( CameraComponent camera, in RefParams refParams, bool first )
	{
		var width = Math.Max( refParams.ViewportWidth, 1 );
		var height = Math.Max( refParams.ViewportHeight, 1 );

		camera.WorldPosition = refParams.ViewOrigin;
		camera.WorldRotation = new Angles( refParams.ViewAngles.x, refParams.ViewAngles.y, refParams.ViewAngles.z );
		camera.FovAxis = CameraComponent.Axis.Horizontal;
		camera.FieldOfView = ScreenFieldOfView( FieldOfView, (float)width / height );
		camera.Viewport = new Vector4( (float)refParams.ViewportX / ScreenWidth, (float)refParams.ViewportY / ScreenHeight, (float)width / ScreenWidth, (float)height / ScreenHeight );

		var onlyClientDraw = refParams.OnlyClientDraw != 0;

		if ( onlyClientDraw != camera.RenderTags.Has( OnlyClientDrawTag ) )
		{
			if ( onlyClientDraw )
				camera.RenderTags.Add( OnlyClientDrawTag );
			else
				camera.RenderTags.Remove( OnlyClientDrawTag );
		}

		if ( first )
		{
			if ( onlyClientDraw != _firstViewOnlyClientDraw )
			{
				_firstViewOnlyClientDraw = onlyClientDraw;

				if ( onlyClientDraw )
				{
					_firstViewBackground = camera.BackgroundColor;
					camera.BackgroundColor = Color.Black;
				}
				else
				{
					camera.BackgroundColor = _firstViewBackground;
				}
			}
		}
		else
		{
			camera.ClearFlags = onlyClientDraw ? ClearFlags.Depth | ClearFlags.Stencil : ClearFlags.All;
			camera.BackgroundColor = Color.Black;
		}
	}

	private void RenderNextViews( CameraComponent camera, ref RefParams refParams, Vector3 paramOrigin, Vector3 paramAngles )
	{
		if ( !camera.RenderExcludeTags.Has( ViewTag( 1 ) ) )
			camera.RenderExcludeTags.Add( ViewTag( 1 ) );

		if ( refParams.NextView == 0 )
		{
			if ( _nextViewCamera.IsValid() && _nextViewCamera.Enabled )
			{
				_nextViewCamera.Enabled = false;
				_nextViewWorld = false;

				ClearTriangles( 1 );
				HideViewEffects( 1 );
			}

			return;
		}

		refParams.ViewOrigin = paramOrigin;
		refParams.ViewAngles = paramAngles;

		_clientDll.CalcRefdef( ref refParams );

		ViewAngles = refParams.ClientViewAngles;

		if ( !refParams.Intermission && !refParams.Paused && ChaseActive != 0f )
			ChaseUpdate( ref refParams );

		_viewForward = refParams.Forward;
		_viewRight = refParams.Right;
		_viewUp = refParams.Up;

		_viewOrigin = refParams.ViewOrigin;
		_viewAngles = refParams.ViewAngles;
		_viewModelOrigin = refParams.ViewModelOrigin;
		_viewModelAngles = refParams.ViewModelAngles;

		if ( !_nextViewCamera.IsValid() )
		{
			var gameObject = new GameObject( true, "next view" );

			gameObject.NetworkMode = NetworkMode.Never;

			_nextViewCamera = gameObject.AddComponent<CameraComponent>();
			_nextViewCamera.IsMainCamera = false;
			_nextViewCamera.RenderExcludeTags.Add( ViewTag( 0 ) );

			gameObject.AddComponent<GammaSpace>();
		}

		_nextViewCamera.Enabled = true;
		_nextViewCamera.Priority = camera.Priority + 1;
		_nextViewCamera.ZNear = camera.ZNear;
		_nextViewCamera.ZFar = camera.ZFar;

		SetupViewCamera( _nextViewCamera, refParams, false );

		MathLib.AngleVectors( refParams.ViewAngles, out var forward, out var right, out var up );

		var tanHalfX = MathF.Tan( _nextViewCamera.FieldOfView * 0.008726646f );
		var tanHalfY = tanHalfX * Math.Max( refParams.ViewportHeight, 1 ) / Math.Max( refParams.ViewportWidth, 1 );

		_nextViewWorld = refParams.OnlyClientDraw == 0;

		if ( _nextViewWorld )
		{
			SetFrustum( _nextViewFrustum, refParams.ViewOrigin, forward, right, up, FieldOfView, Math.Max( refParams.ViewportWidth, 1 ), Math.Max( refParams.ViewportHeight, 1 ) );
			MarkViewLeaves( refParams.ViewOrigin );

			_drawView = 1;

			DrawParticles( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
			DrawBeams( refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );

			_drawView = 0;
		}
		else
		{
			HideViewEffects( 1 );
		}

		DrawTriangles( 1, _nextViewCamera, refParams.ViewOrigin, forward, right, up, tanHalfX, tanHalfY );
	}

	private static float ScreenFieldOfView( float fieldOfView, float aspect )
	{
		if ( WidescreenYFov != 0f && aspect > 1.5f )
		{
			var half = (float)(Math.Tan( fieldOfView * 0.008726646f ) * (aspect * 0.75f));
			var degrees = (float)(Math.Atan( half ) * 57.29577951308232);

			fieldOfView = degrees + degrees;
		}

		if ( fieldOfView < 1f || fieldOfView > 179f )
			fieldOfView = 90f;

		return fieldOfView;
	}
}
