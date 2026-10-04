namespace GoldSrc.Studio;

public sealed class BoneOutput
{
	private const int FxRows = 3;

	private Vector4[] _origins;
	private Sandbox.Texture _originTexture;
	private Vector4[] _fx;
	private Sandbox.Texture _fxTexture;
	private BBox _bounds;

	public void UpdateBounds( SkinnedModelRenderer renderer )
	{
		if ( renderer is { Enabled: true, SceneModel: { } sceneModel } )
			sceneModel.Bounds = _bounds;
	}

	private void SetFxRow( int bone, int row, Vector4 value ) => _fx[(row * BoneSetup.MaxStudioBones) + bone] = value;

	private Vector4 FxRow( int bone, int row ) => _fx[(row * BoneSetup.MaxStudioBones) + bone];

	private Vector3 FxPoint( int bone, Vector3 point )
	{
		return new Vector3(
			Vector3.Dot( (Vector3)FxRow( bone, 0 ), point ) + FxRow( bone, 0 ).w,
			Vector3.Dot( (Vector3)FxRow( bone, 1 ), point ) + FxRow( bone, 1 ).w,
			Vector3.Dot( (Vector3)FxRow( bone, 2 ), point ) + FxRow( bone, 2 ).w );
	}

	private bool FxTransform( Client client, int bone, Transform root, RenderFx renderFx, float animTime )
	{
		var origin = root.Position;

		SetFxRow( bone, 0, new Vector4( 1f, 0f, 0f, 0f ) );
		SetFxRow( bone, 1, new Vector4( 0f, 1f, 0f, 0f ) );
		SetFxRow( bone, 2, new Vector4( 0f, 0f, 1f, 0f ) );

		switch ( renderFx )
		{
			case RenderFx.Distort:
			case RenderFx.Hologram:
				if ( Server.RandomLong( 0, 49 ) == 0 )
				{
					var axis = Server.RandomLong( 0, 1 );

					if ( axis == 1 )
						axis = 2;

					var scale = Server.RandomFloat( 1f, 1.484f );
					var row = FxRow( bone, axis );

					SetFxRow( bone, axis, new Vector4( (Vector3)row * scale, origin[axis] * (1f - scale) ) );

					return true;
				}

				if ( Server.RandomLong( 0, 49 ) == 0 )
				{
					Server.RandomLong( 0, 1 );

					var offset = Server.RandomFloat( -10f, 10f );
					var axis = Server.RandomLong( 0, 2 );
					var row = FxRow( bone, axis );

					SetFxRow( bone, axis, row with { w = row.w + offset } );

					return true;
				}

				return false;

			case RenderFx.Explode:
			{
				var scale = (float)(((client.ClientTime - animTime) * 10.0) + 1.0);

				if ( scale > 2f )
					scale = 2f;

				var axis = root.Rotation * new Vector3( 0f, 1f, 0f );

				for ( var i = 0; i < FxRows; i++ )
				{
					var row = FxRow( bone, i ) + new Vector4( axis * (axis[i] * (scale - 1f)), 0f );

					SetFxRow( bone, i, row with { w = origin[i] - Vector3.Dot( (Vector3)row, origin ) } );
				}

				return true;
			}
		}

		return false;
	}

	private bool SetupFx( Client client, File header, Transform world, Transform[] transforms, RenderFx renderFx, float animTime )
	{
		if ( renderFx is not (RenderFx.Distort or RenderFx.Hologram or RenderFx.Explode) )
			return false;

		_fx ??= new Vector4[BoneSetup.MaxStudioBones * FxRows];

		var active = false;

		for ( var i = 0; i < header.Bones.Length; i++ )
		{
			var parent = header.Bones[i].Parent;

			if ( parent == -1 )
			{
				active |= FxTransform( client, i, world.ToWorld( transforms[i] ), renderFx, animTime );
				continue;
			}

			for ( var row = 0; row < FxRows; row++ )
				SetFxRow( i, row, FxRow( parent, row ) );
		}

		return active;
	}

	private static int FindCachedBone( Client client, string name )
	{
		for ( var i = 0; i < client.CachedBones; i++ )
		{
			if ( name.Equals( client.CachedBoneNames[i], System.StringComparison.OrdinalIgnoreCase ) )
				return i;
		}

		return -1;
	}

	public void Apply( Client client, File header, SkinnedModelRenderer renderer, Transform world, Vector3[] attachments, RenderFx renderFx = RenderFx.None, float animTime = 0f, bool merge = false )
	{
		if ( renderer.SceneModel is not { } sceneModel )
			return;

		sceneModel.Transform = world;

		var bones = client.BoneSetup;
		var transforms = client.BoneTransforms;

		for ( var i = 0; i < header.Bones.Length; i++ )
		{
			var cached = merge ? FindCachedBone( client, header.Bones[i].Name ) : -1;

			if ( cached >= 0 )
			{
				transforms[i] = client.CachedBoneTransforms[cached];
			}
			else
			{
				var local = new Transform( bones.Positions[i], bones.Rotations[i] );
				var parent = header.Bones[i].Parent;

				transforms[i] = parent == -1 ? local : transforms[parent].ToWorld( local );
			}

			sceneModel.SetBoneOverride( i, transforms[i] );
		}

		if ( header.Bones.Length > 0 )
		{
			var bounds = BBox.FromPositionAndSize( world.PointToWorld( transforms[0].Position ) );

			for ( var i = 1; i < header.Bones.Length; i++ )
				bounds = bounds.AddPoint( world.PointToWorld( transforms[i].Position ) );

			// Dumb issue: the renderer drops models that get small on screen (r_size_cull_threshold, 0.25%),
			// which GoldSrc doesn't do so an MP5 grenade blinks out about 350 units away.
			// Type "r_size_cull_threshold 0" in the console for a workaround
			_bounds = bounds.Grow( renderer.Model.Bounds.Size.Length );
			sceneModel.Bounds = _bounds;
		}

		var fx = SetupFx( client, header, world, transforms, renderFx, animTime );

		if ( fx )
		{
			_fxTexture ??= Sandbox.Texture.Create( BoneSetup.MaxStudioBones, FxRows, ImageFormat.RGBA32323232F ).WithDynamicUsage().Finish();
			_fxTexture.Update<Vector4>( _fx );

			renderer.Attributes.Set( "BoneFx", _fxTexture );
		}

		renderer.Attributes.Set( "HasBoneFx", fx ? 1f : 0f );

		for ( var i = 0; i < header.Attachments.Length && i < attachments.Length; i++ )
			attachments[i] = world.PointToWorld( transforms[header.Attachments[i].Bone].PointToWorld( header.Attachments[i].Org ) );

		if ( !header.HasChrome )
			return;

		_origins ??= new Vector4[BoneSetup.MaxStudioBones];
		_originTexture ??= Sandbox.Texture.Create( BoneSetup.MaxStudioBones, 1, ImageFormat.RGBA32323232F ).WithDynamicUsage().Finish();

		for ( var i = 0; i < header.Bones.Length; i++ )
			_origins[i] = new Vector4( fx ? FxPoint( i, world.PointToWorld( transforms[i].Position ) ) : world.PointToWorld( transforms[i].Position ), 0f );

		_originTexture.Update<Vector4>( _origins );

		renderer.Attributes.Set( "BoneOrigins", _originTexture );
		renderer.Attributes.Set( "HasBoneOrigins", 1f );
	}
}
