using System.Collections.Generic;
using System.Linq;
using Godot;
using Networking;

public partial class World : Node2D, NetworkPointUser {
    public static World Me;

    [Export] public Biome[] Biomes;

    public TileMapLayer WallsTileMapLayer;

    public NetworkPoint NetworkPoint { get; set; } = new NetworkPoint();

    private Biome _activeBiome;
    private List<WorldGenerator.RoomPlacement> _unloadedRoomPlacements = new List<WorldGenerator.RoomPlacement>();
    private Dictionary<LoadedRoom, float> _loadedRoomPlacements = new Dictionary<LoadedRoom, float>();


    public override void _Ready() {
        Me = this;

        WallsTileMapLayer = GetNode<TileMapLayer>("Walls");

        foreach (Biome biome in Biomes) {
            biome.Load();
        }
    }

    public override void _Process(double delta) {
        foreach (Player player in Player.Players) {
            Load(player.GlobalPosition);
        }

        Unload((float)delta);
    }

    public static void Start() {
        Me._loadedRoomPlacements = new Dictionary<LoadedRoom, float>();

        Me._activeBiome = Me.Biomes[0];

        Stack<WorldGenerator.RoomPlacement> roomPlacements = WorldGenerator.Me.Generate(Game.Seed, Me._activeBiome);

        foreach (WorldGenerator.RoomPlacement roomPlacement in roomPlacements) {
            Me._unloadedRoomPlacements.Add(roomPlacement);

            if (roomPlacement is WorldGenerator.BranchedRoomPlacement branchedRoomPlacement) {
                foreach (Stack<WorldGenerator.RoomPlacement> branches in branchedRoomPlacement.BranchRoomPlacements) {
                    foreach (WorldGenerator.RoomPlacement branchRoomPlacement in branches) {
                        Me._unloadedRoomPlacements.Add(branchRoomPlacement);
                    }
                }
            }
        }

        Me.Load(Vector2.Zero);
    }

    private void Load(Vector2 location) {
        List<LoadedRoom> loadedRooms = _loadedRoomPlacements.Keys.ToList();

        foreach (LoadedRoom room in loadedRooms) {
            if (location.DistanceTo(room.RoomPlacement.Location * 16) > 600) continue;
            // if (location.DistanceTo(room.RoomPlacement.Location * 16) > 100) continue;

            _loadedRoomPlacements[room] = 10;
        }

        for (int index = 0; index < _unloadedRoomPlacements.Count; index++) {
            WorldGenerator.RoomPlacement roomPlacement = _unloadedRoomPlacements[index];

            if (location.DistanceTo(roomPlacement.Location * 16) > 600) continue;
            // if (location.DistanceTo(roomPlacement.Location * 16) > 100) continue;

            _unloadedRoomPlacements.RemoveAt(index);
            index--;

            LoadedRoom loadedRoom = new LoadedRoom(roomPlacement, this, _activeBiome);

            _loadedRoomPlacements.Add(loadedRoom, 10);

            loadedRoom.Load();
        }
    }

    private void Unload(float delta) {
        List<LoadedRoom> loadedRooms = _loadedRoomPlacements.Keys.ToList();

        foreach (LoadedRoom room in loadedRooms) {
            _loadedRoomPlacements[room] -= delta;

            if (_loadedRoomPlacements[room] > 0) {
                room.Update();

                continue;
            }

            _loadedRoomPlacements.Remove(room);

            _unloadedRoomPlacements.Add(room.RoomPlacement);

            room.Unload();
        }
    }

    // private void PlaceRoom(WorldGenerator.RoomPlacement placement) {
    //     foreach (Vector2 tileLocation in placement.RoomLayout.Walls) {
    //         Vector2I realTileLocation = placement.Location + new Vector2I((int)tileLocation.X, (int)tileLocation.Y);

    //         WallsTileMapLayer.SetCell(realTileLocation, 0, new Vector2I(3, 0));
    //     }

    //     if (placement is WorldGenerator.BranchedRoomPlacement branchPlacement) {
    //         foreach (Stack<WorldGenerator.RoomPlacement> branchStack in branchPlacement.BranchRoomPlacements) {
    //             foreach (WorldGenerator.RoomPlacement branchRoomPlacement in branchStack) {
    //                 foreach (Vector2 tileLocation in branchRoomPlacement.RoomLayout.Walls) {
    //                     Vector2I realTileLocation = branchRoomPlacement.Location + new Vector2I((int)tileLocation.X, (int)tileLocation.Y);

    //                     WallsTileMapLayer.SetCell(realTileLocation, 0, new Vector2I(3, 0));
    //                 }
    //             }
    //         }
    //     }
    // }
}
