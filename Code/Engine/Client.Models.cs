namespace GoldSrc;

partial class Client
{
	public Studio.BoneSetup BoneSetup { get; } = new();
	public Transform[] BoneTransforms { get; } = new Transform[Studio.BoneSetup.MaxStudioBones];

	public string[] CachedBoneNames { get; } = new string[Studio.BoneSetup.MaxStudioBones];
	public Transform[] CachedBoneTransforms { get; } = new Transform[Studio.BoneSetup.MaxStudioBones];
	public int CachedBones { get; private set; }

	private Sandbox.Model _spriteQuad;

	public void SaveBones( Studio.File header )
	{
		CachedBones = header.Bones.Length;

		for ( var i = 0; i < CachedBones; i++ )
		{
			CachedBoneNames[i] = header.Bones[i].Name;
			CachedBoneTransforms[i] = BoneTransforms[i];
		}
	}

	public Sandbox.Model SpriteQuad => _spriteQuad ??= CreateSpriteQuad();

	private static Sandbox.Model CreateSpriteQuad()
	{
		var mesh = new Mesh( Material.Load( "materials/default.vmat" ) );

		mesh.CreateVertexBuffer( 4, new List<Vertex>
		{
			new( new Vector3( 0f, 0f, 0f ), Vector3.Forward, Vector3.Right, new Vector4( 0f, 1f, 0f, 0f ) ),
			new( new Vector3( 0f, 0f, 1f ), Vector3.Forward, Vector3.Right, new Vector4( 0f, 0f, 0f, 0f ) ),
			new( new Vector3( 0f, 1f, 1f ), Vector3.Forward, Vector3.Right, new Vector4( 1f, 0f, 0f, 0f ) ),
			new( new Vector3( 0f, 1f, 0f ), Vector3.Forward, Vector3.Right, new Vector4( 1f, 1f, 0f, 0f ) )
		} );

		mesh.CreateIndexBuffer( 6, new List<int> { 0, 2, 1, 0, 3, 2 } );
		mesh.Bounds = new BBox( Vector3.Zero, new Vector3( 0f, 1f, 1f ) );

		return Sandbox.Model.Builder.AddMesh( mesh ).Create();
	}
}
