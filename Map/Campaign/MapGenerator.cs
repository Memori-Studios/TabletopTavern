using System.Collections.Generic;
using UnityEngine;
using Shapes;
using Unity.VisualScripting;
using Memori.Scenes;
using Memori.Audio;
using System.Linq;
using Unity.Mathematics;
using System.Threading.Tasks;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ.Map
{
    [System.Serializable] public struct MapNodeData
    {
        public MapNode mapNodeGameObject; // Reference to the game object of the node
        public int index; // Index of the node in its layer
        public int layer; // Layer or level the node belongs to
        public Vector2 position; // Position for visualization
        public List<int> connectedNodeIndexes; // Indexes of connected nodes in the next row
        public List<Line> connectedNodeLines; // Lines connecting to the next row
        public NodeType type; // Type of node (combat, event, etc.)
    }
    [System.Serializable] public struct MapLayer { public List<MapNodeData> LayerNodes; }
    public enum NodeType { Event, Shop, Town, Skirmish, Warband, Horde, Test, Treasure, Games, Campfire }


    [RequireComponent(typeof(MapSceneManager))]
    [RequireComponent(typeof(TreeSpawner))]
    [RequireComponent(typeof(TerrainTexturePainter))]
    [RequireComponent(typeof(MapNodeFacingManager))]
    public class MapGenerator : MonoBehaviour
    {
        [SerializeField] private MapSceneManager mapSceneManager;

        [Header("Node Overrides")]
        [SerializeField] private NodeType firstNodeTypeBook1 = NodeType.Treasure;
        [SerializeField] private NodeType firstNodeTypeOtherBooks = NodeType.Campfire;
        [SerializeField] private NodeType lastNodeType = NodeType.Horde;

        [Header("Generation")]
        [SerializeField] private int seed = 0; // Random generation seed
        [SerializeField] private int layers = 16; // Number of layers
        [SerializeField] private int nodesPerLayer = 3; // Nodes per layer
        [SerializeField] private float nodeSpacingX = 0.275f; // Spacing between nodes horizontally
        [SerializeField] private float nodeSpacingY = 0.4f; // Spacing between nodes vertically
        [SerializeField] private Vector2 randomOffset = new Vector2(0.025f, 0.037f); // Random position offset
        [SerializeField] private float hiddenNodeChance = 0.25f; // Chance for a node to be hidden
        [SerializeField] private List<MapLayer> mapLayers = new(); // Layers of the map
        [SerializeField] private LayerNodeProbability[] layerNodeTypeWeights;

        [Header("Visuals")]
        [SerializeField] private Transform mapParent;
        [SerializeField] private MapNode mapNodePrefab;
        [SerializeField] private Color pathDefaultColor;

        [Header("Testing")]
        [SerializeField] private MapRegion _testMapRegion;
        [SerializeField] private Race _testRace;

        //paths
        GameObject[] nodeGameObjects;
        List<Path> paths;
        
        TreeSpawner treeSpawner;
        Camera mapSceneCamera;
        Vector2 startNodePosition = new Vector2(0f, -0.3f);
        Vector2 finalNodePosition = new Vector2(4.15f, -0.3f);
        float leftNodeAvg = -0.15f, rightNodeAvg = -0.45f;
        MapRegion mapRegion;
        int _bookNumber = 0;
        Race _race;
        public Race MapRace => _race;
        public async void LoadMap(int bookNumber)
        {
#if !UNITY_EDITOR
            Debug.Log($"MapGenerator: Loading map with seed {CampaignManager.Instance.CampaignSaveManager.SaveData.seed}");
            firstNodeTypeBook1 = NodeType.Treasure;
            firstNodeTypeOtherBooks = NodeType.Campfire;
            lastNodeType = NodeType.Horde;
#endif
            seed = CampaignManager.Instance.CampaignSaveManager.SaveData.seed;
            await GenerateMap(bookNumber);

            int heroId = CampaignManager.Instance.CampaignSaveManager.SaveData.heroID;
            Race heroRace = HeroData.GetRaceFromHero(heroId);
            IAudioRequester.Instance.SetAudioThemePack((int)heroRace);

            SceneHandler.Instance.AlertOfSceneSetUpComlete();
            TutorialManager.Instance.DelayedStart();
            
            mapSceneManager.CompleteLoad();
        }
        public void ClearMap()
        {
            for (int i = mapParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(mapParent.GetChild(i).gameObject);
            }
            treeSpawner.ClearTrees();
            GetComponent<TerrainTexturePainter>().ClearTerrain();

        }
        public async Task PruneTrees()
        {
            await treeSpawner.PruneTrees();
        }
        public async Task GenerateMap(int bookNumber)
        {
            _bookNumber = bookNumber;
            GenerateRace();

            SeededRandom.Init(seed + (bookNumber * 13));
#if UNITY_EDITOR
            if (!Application.isPlaying) ConsoleUtility.ClearConsole();
#endif

            treeSpawner = GetComponent<TreeSpawner>();
            mapSceneCamera = GetComponent<MapSceneManager>().MapScecneCamera;
            GetComponent<MapNodeFacingManager>().SetCamera(mapSceneCamera);
            if(Application.isPlaying)
            {
                mapRegion = MapThemeManager.Instance.GetMapRegion(_race);
            }
            else
            {
                mapRegion = _testMapRegion;
            }
            BuildLayers();
            DrawMap();
            GetComponent<MapSceneManager>().SetMapLayers(mapLayers);
            PaintTextures(mapRegion);
            await SpawnTerrainDetails(mapRegion);
            await treeSpawner.PruneTrees();

            Debug.Log($"MapGenerator: Finished generating map with seed {seed}");
            return;
        }
        /// <summary>
        /// The node graph alone: layers, types, connections. Draws nothing, so a test can run it on a
        /// bare component. Every seeded draw happens here in the same order as before the split.
        /// </summary>
        internal void BuildLayers()
        {
            mapLayers.Clear();

            Vector2 GetRandomOffset() =>
                new(SeededRandom.Range(-randomOffset.x, randomOffset.x),
                    SeededRandom.Range(-randomOffset.y, randomOffset.y));

            // Generate each layer
            for (int i = 0; i < layers; i++)
            {
                MapLayer currentLayer = new() { LayerNodes = new() };

                for (int j = 0; j < nodesPerLayer; j++)
                {
                    var node = new MapNodeData
                    {
                        index = j + (i * (nodesPerLayer)),
                        layer = i,
                        position = new Vector2(i * nodeSpacingX, j * -nodeSpacingY) + GetRandomOffset(),
                        type = GetRandomNodeType(i),
                        connectedNodeIndexes = new(),
                        connectedNodeLines = new()
                    };

                    currentLayer.LayerNodes.Add(node);
                }

                mapLayers.Add(currentLayer);

                if (i == 0)
                {
                    TrimFirstLayerToOneNode(currentLayer);
                }
                else if (i == layers - 1)
                {
                    TrimLastLayerToOneNode(currentLayer);
                }
                else
                {
                    ConnectLayers(mapLayers[i - 1], currentLayer);
                }
            }
            // Debug.Log($"MapGenerator: Finished generating layers");
            ConnectSecondLastLayerToFinalNode();
            RemoveNodeAndTransferConnections();
            FixIndexing();
            AddAdditionalConnections();
            // ReplaceRepeatingNodes();
            // ReplaceEarlyEliteFights();
            // EnforceLayerTypeDiversity();
            CenterLayersWithOnlyTwoNodes();
        }
        /// <summary>Seeds the stream and builds the graph for one book, the way GenerateMap does before drawing.</summary>
        internal void BuildLayers(int seed, int bookNumber)
        {
            this.seed = seed;
            _bookNumber = bookNumber;
            SeededRandom.Init(seed + (bookNumber * 13));
            BuildLayers();
        }
        internal IReadOnlyList<MapLayer> Layers => mapLayers;
        private void FixIndexing()
        {
            //look at connected node index, get the actual index of the node from that layer
            for (int i = 0; i < mapLayers.Count; i++)
            {
                if (i == mapLayers.Count - 1) continue; // last layer has no next layer
                for (int j = 0; j < mapLayers[i].LayerNodes.Count; j++)
                {
                    for (int k = 0; k < mapLayers[i].LayerNodes[j].connectedNodeIndexes.Count; k++)
                    {
                        int connectedIndex = mapLayers[i].LayerNodes[j].connectedNodeIndexes[k];
                        if(connectedIndex >= mapLayers[i + 1].LayerNodes.Count) {
                            connectedIndex = mapLayers[i + 1].LayerNodes.Count - 1;
                        }
                        mapLayers[i].LayerNodes[j].connectedNodeIndexes[k] = mapLayers[i + 1].LayerNodes[connectedIndex].index;
                        // Debug.Log($"Connected index: {connectedIndex}, actual index: {mapLayers[i + 1].LayerNodes[connectedIndex].index}");
                    }
                }
            }
        }
        private void AddAdditionalConnections()
        {
            //itterate through the 3 nodes of each layer, and make sure that they are connected to the node with the same index of the layer below
            for (int i = 0; i < mapLayers.Count - 2; i++)
            {
                //if layer has only 1 or 2 nodes, do not add connections
                if (mapLayers[i].LayerNodes.Count <= 2) continue;

                // Possible connections within a layer: 0→1, 1→2, 1→0, 2→1
                (int fromIndex, int toIndex)[] possibleConnections = new (int, int)[]
                {
                    (0, 1), // Node 0 to Node 1
                    (1, 2), // Node 1 to Node 2
                    (1, 0), // Node 1 to Node 0
                    (2, 1)  // Node 2 to Node 1
                };

                //25% chance to add 1 connection, 75% chance to add 2 connections
                int connectionCount = SeededRandom.Range(0, 1f) < 0.25f ? 1 : 2;

                // Shuffle the possible connections to pick randomly
                var shuffledConnections = possibleConnections.OrderBy(x => SeededRandom.Range(0, 1f)).ToList();
                for (int k = 0; k < connectionCount; k++)
                {
                    var connection = shuffledConnections[k];
                    int fromNodeIndex = connection.fromIndex;
                    int toNodeIndex = connection.toIndex;

                    // Add the connection
                    // Debug.Log($"Adding connection between layer {i} node {fromNodeIndex} to layer {i+1} node {toNodeIndex}");
                    //make sure layernodes is long enough to access the index
                    if (mapLayers[i].LayerNodes.Count <= fromNodeIndex || mapLayers[i + 1].LayerNodes.Count <= toNodeIndex) {
                        // Debug.LogError($"Layer {i} or {i+1} does not have enough nodes to access index {fromNodeIndex} or {toNodeIndex}");
                        continue;
                    }
                    mapLayers[i].LayerNodes[fromNodeIndex].connectedNodeIndexes.Add(mapLayers[i + 1].LayerNodes[toNodeIndex].index);

                    // Remove the conflicting connection to prevent crossing
                    (int, int) conflictingConnection = (toNodeIndex, fromNodeIndex); // Reverse of the added connection
                    shuffledConnections.Remove(conflictingConnection);

                    // Ensure we don't try to access more connections than available
                    if (k + 1 >= shuffledConnections.Count) break;
                }
            }
        }
        private void TrimFirstLayerToOneNode(MapLayer firstLayer)
        {
            firstLayer.LayerNodes.RemoveRange(1, firstLayer.LayerNodes.Count - 1);
            var singleNode = firstLayer.LayerNodes[0];
            // Endless acts open on the act 1 node (Treasure): the spoils of the war just won.
            bool treasureOpener = _bookNumber == 1 || _bookNumber > TabletopTavernConstants.FINAL_STORY_ACT;
            singleNode.type = treasureOpener ? firstNodeTypeBook1 : firstNodeTypeOtherBooks;
            singleNode.position = startNodePosition +
                                    new Vector2(SeededRandom.Range(-randomOffset.x, randomOffset.x),
                                                SeededRandom.Range(-randomOffset.y, randomOffset.y));
            firstLayer.LayerNodes[0] = singleNode;
        }
        private void TrimLastLayerToOneNode(MapLayer lastLayer)
        {
            lastLayer.LayerNodes.RemoveRange(1, lastLayer.LayerNodes.Count - 1);
            var singleNode = lastLayer.LayerNodes[0];
            singleNode.type = lastNodeType;
            singleNode.position = finalNodePosition + 
            // singleNode.position = new Vector2(singleNode.position.x, -0.5f) + 
                                    new Vector2(SeededRandom.Range(-randomOffset.x, randomOffset.x),
                                                SeededRandom.Range(-randomOffset.y, randomOffset.y));
            lastLayer.LayerNodes[0] = singleNode;
        }
        private void ConnectLayers(MapLayer previousLayer, MapLayer currentLayer)
        {
            for (int i = 0; i < currentLayer.LayerNodes.Count; i++)
            {
                // Ensure index connections align between layers
                int previousIndex = Mathf.Min(i, previousLayer.LayerNodes.Count - 1);

                // Add the index of the corresponding node in the next layer
                previousLayer.LayerNodes[previousIndex].connectedNodeIndexes.Add(i);
            }
        }
        private void ConnectSecondLastLayerToFinalNode()
        {
            if (layers <= 1) {
                Debug.LogError($"Cannot connect second last layer to final node with {layers} layers");
                return;
            }

            var secondLastLayer = mapLayers[layers - 2];

            foreach (var node in secondLastLayer.LayerNodes)
            {
                if (!node.connectedNodeIndexes.Contains(0))
                {
                    node.connectedNodeIndexes.Add(0); // Connect to the only node in the last layer
                }
            }
        }
        private void RemoveNodeAndTransferConnections()
        {
            for (int i = 0; i < mapLayers.Count - 2; i++)
            {
                if (mapLayers[i].LayerNodes.Count <= 1) continue;

                //only 25% chance to remove a node
                if (SeededRandom.Range(0, 1f) > 0.25f) continue;

                int indexToRemove = SeededRandom.Range(0, mapLayers[i].LayerNodes.Count);
                var nodeToRemove = mapLayers[i].LayerNodes[indexToRemove];

                // Before removing, fix incoming connections from the previous layer.
                // ConnectLayers stored positions (0,1,2) into connectedNodeIndexes. After
                // removal the list compacts, so those positions must be updated now while
                // they still correspond 1-to-1 with the current layer's node positions.
                if (i > 0)
                {
                    var prevLayer = mapLayers[i - 1];
                    for (int n = 0; n < prevLayer.LayerNodes.Count; n++)
                    {
                        // connectedNodeIndexes is a List<int> (reference type), so edits
                        // here apply to the original even though LayerNodes is a struct list.
                        List<int> conns = prevLayer.LayerNodes[n].connectedNodeIndexes;
                        for (int c = conns.Count - 1; c >= 0; c--)
                        {
                            if (conns[c] == indexToRemove)
                            {
                                // Redirect to nearest surviving neighbor
                                int redirect = indexToRemove > 0 ? indexToRemove - 1 : indexToRemove + 1;
                                if (conns.Contains(redirect))
                                    conns.RemoveAt(c);   // already connected there — drop duplicate
                                else
                                    conns[c] = redirect;
                            }
                            else if (conns[c] > indexToRemove)
                            {
                                conns[c]--;              // shift down to match post-removal positions
                            }
                        }
                    }
                }

                TransferConnections(mapLayers[i], nodeToRemove, indexToRemove);
                mapLayers[i].LayerNodes.RemoveAt(indexToRemove);
            }
        }
        private void CenterLayersWithOnlyTwoNodes()
        {
            for (int i = 0; i < mapLayers.Count; i++)
            {
                if (mapLayers[i].LayerNodes.Count == 2)
                {
                    // Debug.Log($"Centering layer {i} with only two nodes.");
                    MapNodeData leftNode = mapLayers[i].LayerNodes[0];
                    leftNode.position.y = leftNodeAvg;
                    mapLayers[i].LayerNodes[0] = leftNode;

                    MapNodeData rightNode = mapLayers[i].LayerNodes[1];
                    rightNode.position.y = rightNodeAvg;
                    mapLayers[i].LayerNodes[1] = rightNode;
                }
            }
        }
        private void TransferConnections(MapLayer layer, MapNodeData nodeToRemove, int indexToRemove)
        {
            if (indexToRemove > 0)
            {
                var leftNeighbor = layer.LayerNodes[indexToRemove - 1];
                foreach (int connectedIndex in nodeToRemove.connectedNodeIndexes)
                {
                    if (!leftNeighbor.connectedNodeIndexes.Contains(connectedIndex))
                    {
                        leftNeighbor.connectedNodeIndexes.Add(connectedIndex);
                    }
                }
            }

            if (indexToRemove < layer.LayerNodes.Count - 1)
            {
                var rightNeighbor = layer.LayerNodes[indexToRemove + 1];
                foreach (int connectedIndex in nodeToRemove.connectedNodeIndexes)
                {
                    if (!rightNeighbor.connectedNodeIndexes.Contains(connectedIndex))
                    {
                        rightNeighbor.connectedNodeIndexes.Add(connectedIndex);
                    }
                }
            }
        }
        // private static readonly HashSet<NodeType> SpecialNodeTypes = new()
        // {
        //     NodeType.Shop, NodeType.Event, NodeType.Town, NodeType.Treasure, NodeType.Games
        // };
        // private void EnforceLayerTypeDiversity()
        // {
        //     for (int i = 1; i < mapLayers.Count - 1; i++)
        //     {
        //         int specialCount = 0;
        //         for (int j = 0; j < mapLayers[i].LayerNodes.Count; j++)
        //         {
        //             if (SpecialNodeTypes.Contains(mapLayers[i].LayerNodes[j].type))
        //                 specialCount++;
        //         }

        //         if (specialCount <= 1) continue;

        //         bool firstSpecialKept = false;
        //         for (int j = 0; j < mapLayers[i].LayerNodes.Count; j++)
        //         {
        //             MapNodeData node = mapLayers[i].LayerNodes[j];
        //             if (!SpecialNodeTypes.Contains(node.type)) continue;

        //             if (!firstSpecialKept) { firstSpecialKept = true; continue; }

        //             node.type = NodeType.Campfire;
        //             mapLayers[i].LayerNodes[j] = node;
        //         }
        //     }
        // }
        private NodeType GetRandomNodeType(int _layer)
        {
            LayerNodeProbability possibleNodeTypes = layerNodeTypeWeights[_layer];
            float weight = SeededRandom.Range(0, 1f);

            for(int i = 0; i < possibleNodeTypes.nodeTypeWeights.Length; i++) {
                if (weight <= possibleNodeTypes.nodeTypeWeights[i].weight) {
                    return possibleNodeTypes.nodeTypeWeights[i].type;
                }
            }
            Debug.LogError($"Failed to get node type, returning default: {NodeType.Horde}");
            return NodeType.Horde;
        }
        private void GenerateRace()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _race = _testRace;
                return;
            }
#endif
            int heroID = CampaignManager.Instance.CampaignSaveManager.SaveData.heroID;
            Race heroRace = HeroData.GetRaceFromHero(heroID);
            _race = TabletopTavernData.Instance.GenerateRaceForMap(_bookNumber, seed, heroRace);
        }
        private void DrawMap()
        {
            // Destroy all existing children
            for (int i = mapParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(mapParent.GetChild(i).gameObject);
            }

            int x=0;
            // Instantiate nodes
            foreach (var layer in mapLayers)
            {
                for(int i = 0; i < layer.LayerNodes.Count; i++)
                {
                    MapNodeData node = layer.LayerNodes[i];
                    MapNode nodeObject = null;

                    #if UNITY_EDITOR
                        nodeObject = PrefabUtility.InstantiatePrefab(mapNodePrefab, mapParent).GetComponent<MapNode>();
                    #else
                        nodeObject = Instantiate(mapNodePrefab, mapParent);
                    #endif

                    bool hidden = SeededRandom.Range(0, 1f) < hiddenNodeChance;
                    if (x < layerNodeTypeWeights.Length && layerNodeTypeWeights[x].preventHidden) {
                        hidden = false;
                    }
                    nodeObject.SetUp(node, mapSceneCamera, hidden, _race);

                    nodeObject.transform.localPosition = new Vector3(node.position.x, 0, node.position.y);
                
                    nodeObject.name = $"Node ({node.layer} - {node.type})";
                    node.mapNodeGameObject = nodeObject;
                    layer.LayerNodes[i] = node;
                }
                x++;
            }
            nodeGameObjects = new GameObject[mapParent.transform.childCount];
            for (int i = 0; i < mapParent.transform.childCount; i++) {
                nodeGameObjects[i] = mapParent.GetChild(i).gameObject;
            }
            paths = new List<Path>();

            // Draw connections safely
            for (int i = 0; i < mapLayers.Count - 1; i++)
            {
                MapLayer currentLayer = mapLayers[i];
                MapLayer nextLayer = mapLayers[i + 1];

                for(int j = 0; j < currentLayer.LayerNodes.Count; j++)
                {
                    MapNodeData node = currentLayer.LayerNodes[j];
                    MapNodeData connectedNodeLayer = nextLayer.LayerNodes[^1];
            
                    foreach (int connectedIndex in node.connectedNodeIndexes)
                    {
                        MapNodeData connectedNode = new MapNodeData();
                        for(int k = 0; k < nextLayer.LayerNodes.Count; k++) {
                            if (nextLayer.LayerNodes[k].index == connectedIndex) {
                                connectedNode = nextLayer.LayerNodes[k];
                            }
                        }

                        GameObject lineObject = new ("Line");
                        var line = lineObject.AddComponent<Line>();
                        lineObject.transform.SetParent(mapParent);
                        lineObject.transform.localPosition = Vector3.zero;
                        Vector3 startPos = new Vector3(node.position.x, -0.01f, node.position.y);
                        Vector3 endPos = new Vector3(connectedNode.position.x, -0.01f, connectedNode.position.y);

                        // Calculate midpoint and direction
                        Vector3 midpoint = (startPos + endPos) / 2;
                        Vector3 direction = (endPos - startPos).normalized;
                        float distance = Vector3.Distance(startPos, endPos);

                        //I want the line to be shorter than just startPos and endPos, so I will set the start and end positions to be 0.5f shorter than the distance
                        startPos = midpoint - (direction * (distance / 2.75f));
                        endPos = midpoint + (direction * (distance / 2.75f));
                        paths.Add(new Path { start = node.mapNodeGameObject.transform.position , end = connectedNode.mapNodeGameObject.transform.position });

                        line.Dashed = true;
                        line.DashSize = 4;
                        line.Start = startPos;
                        line.End = endPos;
                        line.Color = pathDefaultColor;
                        line.Thickness = 0.005f;
                        line.Geometry = LineGeometry.Volumetric3D;
                        line.SortingOrder = -1;


                        // Create collider object to match the line
                        GameObject colliderObject = new("ConnectionCollider");
                        colliderObject.transform.SetParent(mapParent);

                        // Set collider position and orientation
                        colliderObject.transform.localPosition = midpoint;
                        colliderObject.transform.rotation = Quaternion.LookRotation(direction);

                        // Add and configure BoxCollider
                        BoxCollider boxCollider = colliderObject.AddComponent<BoxCollider>();
                        boxCollider.size = new Vector3(0.05f, 0.05f, distance + 0.05f); // Thickness in X/Y, length in Z

                        node.connectedNodeLines.Add(line);
                    }
                }
            }
        }
        public async Task SpawnTerrainDetails(MapRegion mapRegion)
        {
            SeededRandom.Init(seed);
            await treeSpawner.SpawnTrees(mapRegion);
        }
        public void PaintTextures(MapRegion mapRegion)
        {
            GetComponent<TerrainTexturePainter>().PaintTextures(nodeGameObjects, paths, mapRegion);
        }
    }
}
