using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using SFrame;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace BuildMode
{
    public class GridManager : MonoSingleton<GridManager>
    {
        public GameObject testPeoplePrefab;

        [LabelText("Debug")]
        public bool isDebug = true;

        private GridObject[,] gridArray;
        private BuildUnit[,] buildUnitArray;
        private GridMetas meta;
        private GameStates states;
        private GridObject currentGo;
        //寻路图总图，每个格子要保存自己的起点节点，初始化每个网格周围8个节点，去重，设置每个格子的1阶临近，造房子时让坑位初始化自己
        private List<PathFindingNode> nodeList = new List<PathFindingNode>();

        public System.Action updateAction;

        protected override void Awake()
        {
            base.Awake();
        }
        private void Start()
        {
            meta = DataManager.Instance.meta;
            states = DataManager.Instance.states;
            InitGrids();
            InitPathFinding();
            //BindableProperty事件绑定
            states.gameState.beforeValueChanged += BeforeGameStateChang;
            states.currentSelectBuildUnit.beforeValueChanged += BeforeCurrentSelectedBuildUnitChange;
            InitGamePlay();
        }
        private void OnDestroy()
        {
            //BindableProperty事件解绑
            states.gameState.beforeValueChanged -= BeforeGameStateChang;
            states.currentSelectBuildUnit.beforeValueChanged -= BeforeCurrentSelectedBuildUnitChange;
        }
        private void Update()
        {
            updateAction?.Invoke();
        }

        #region 寻路专用
        private void InitPathFinding()
        {
            //先生成方块的四角
            float nodeIntervalX = meta.buildingUnitSize.x * meta.gridUnitLength + meta.buildingUnitInterval;
            float nodeIntervalZ = meta.buildingUnitSize.y * meta.gridUnitLength + meta.buildingUnitInterval;
            Vector3 startPos = new Vector3(-0.5f * (meta.gridUnitLength + meta.buildingUnitInterval), 0.0f, -0.5f * (meta.gridUnitLength + meta.buildingUnitInterval));
            for (int i = 0; i < meta.buildingUnitAmount.x +1; i++)
            {
                float x = startPos.x + i * nodeIntervalX;
                for (int j = 0; j < meta.buildingUnitAmount.y +1; j++)
                {
                    float z = startPos.z + j * nodeIntervalZ;
                    //Instantiate(testPeoplePrefab, new Vector3(x, 0.0f, z), Quaternion.identity);
                    PathFindingNode pfn = new PathFindingNode(new Vector3(x,0.0f,z));
                    nodeList.Add(pfn);
                }
            }
            //纵向插入，并设置引用关系
            int nodeCountOneColumn = meta.buildingUnitAmount.y + 1; // 一列包含的角node数目
            List<PathFindingNode> columInsert = new List<PathFindingNode>();
            for (int i = 1; i < nodeList.Count; i++)
            {
                PathFindingNode down = nodeList[i - 1];
                PathFindingNode up = nodeList[i];
                Vector3 nodePos = new Vector3((up.worldPos.x + down.worldPos.x) / 2f, 0.0f, (up.worldPos.z + down.worldPos.z) / 2f);
                PathFindingNode pfn = new PathFindingNode(nodePos);
                up.neighbors.Add(pfn);
                down.neighbors.Add(pfn);
                pfn.neighbors.Add(up);
                pfn.neighbors.Add(down);
                //设置纵向引用 关系
                columInsert.Add(pfn);
                //Instantiate(testPeoplePrefab, nodePos, Quaternion.identity);
                //设置到地块引用，应在 x-1 y的东方 和 x y 的西方 
                int x = (i - 1) / nodeCountOneColumn;
                int y = (i - 1) % nodeCountOneColumn;
                if (ValidBuildUnitIndex(x - 1, y))
                {
                    GetBuildUnitByIndex(x - 1, y).pathFindingNodes.Add(BuildingDirection.east, pfn);
                }
                if (ValidBuildUnitIndex(x, y))
                {
                    GetBuildUnitByIndex(x, y).pathFindingNodes.Add(BuildingDirection.west, pfn);
                }
                //上部索引+1能整除纵列数目则 索引本轮结束额外+1
                if ((i+1)%nodeCountOneColumn == 0)
                {
                    i++;
                }
            }
            //横向插入，并设置引用关系
            List<PathFindingNode> rowInsert = new List<PathFindingNode>();
            for (int i = nodeCountOneColumn; i < nodeList.Count; i++)
            {
                PathFindingNode left = nodeList[i - nodeCountOneColumn];
                PathFindingNode right = nodeList[i];
                Vector3 nodePos = new Vector3((left.worldPos.x + right.worldPos.x) / 2f, 0.0f, (left.worldPos.z + right.worldPos.z) / 2f);
                PathFindingNode pfn = new PathFindingNode(nodePos);
                left.neighbors.Add(pfn);
                right.neighbors.Add(pfn);
                pfn.neighbors.Add(left);
                pfn.neighbors.Add(right);
                rowInsert.Add(pfn);
                //Instantiate(testPeoplePrefab, nodePos, Quaternion.identity);
                //设置到地块引用，应在 x y-1 的北方 和 x y 的南方 
                int x = (i - nodeCountOneColumn) / nodeCountOneColumn;
                int y = i % nodeCountOneColumn;
                if (ValidBuildUnitIndex(x, y-1))
                {
                    GetBuildUnitByIndex(x, y-1).pathFindingNodes.Add(BuildingDirection.north, pfn);
                }
                if (ValidBuildUnitIndex(x, y))
                {
                    GetBuildUnitByIndex(x, y).pathFindingNodes.Add(BuildingDirection.south, pfn);
                }
            }
            nodeList.AddRange(columInsert);
            nodeList.AddRange(rowInsert);
        }
        #endregion

        #region 临时使用，预览生成房子，清除预览，预览开垦地块
        List<BuildingStructrue> houses = new List<BuildingStructrue>();
        public void SpawnHouses()
        {
            ClearHouses();
            foreach (var item in buildUnitArray)
            {
                BuildingStructrue house = item.PlaceBuilding(new BuildingDataStruct(Random.Range(0,meta.buildingMetaData.TypeCount), Random.Range(0, meta.buildingLevels)));
                houses.Add(house);
            }
        }
        public void SpawnHouses(int typeId)
        {
            ClearHouses();
            foreach (var item in buildUnitArray)
            {
                BuildingStructrue house = item.PlaceBuilding(new BuildingDataStruct(typeId, Random.Range(0, meta.buildingLevels)));
                houses.Add(house);
            }
        }
        public void ClearHouses()
        {
            foreach (var item in houses)
            {
                BuildingModeManager.Instance.ReclaimBuilding(item);
            }
            houses.Clear();
        }
        public void OpenGrids()
        {
            ClearHouses();
            StopAllCoroutines();
            StartCoroutine(OpenOneByOne());
        }
        private WaitForSeconds wait1 = new WaitForSeconds(0.3f);
        private IEnumerator OpenOneByOne()
        {
            for (int i = 0; i <= meta.buildUnitGridTotalAmount; i++)
            {
                foreach (var item in buildUnitArray)
                {
                    item.SetGridOpenProgress(i);
                }
                yield return wait1;
            }
        }
        //选中一个地块，显示UI，先检测是否点击了不允许继续选择网格
        public void SelectOnBuildUnit()
        {
            if (!CheckIfBlockedByUI())
            {
                BuildUnit bu = DetectBuildUnit();
                states.currentSelectBuildUnit.Value = bu;
            }
        }
        private bool CheckIfBlockedByUI()
        {
            return EventSystem.current.IsPointerOverGameObject();
        }
        //游戏性预览，生成初始房子，矿山
        public void InitGamePlay()
        {
            //生成基地（占一个地块），持有引用
            BuildUnit bu = GetBuildUnitByIndex(meta.baseHouseStartGridPos.x, meta.baseHouseStartGridPos.y);
            bu.PlaceBaseHouse();
            states.baseBuildUnit = bu;
            //生成矿山 （不占地块），直接生成在所有网格最北方的中心
            Vector3 minePos = new Vector3(
                (meta.gridSize.x * meta.gridUnitLength + (meta.buildingUnitAmount.x - 1) * meta.buildingUnitInterval) / 2f - 0.5f * meta.gridUnitLength
                , 0.0f,
                (meta.gridSize.y - 0.5f) * meta.gridUnitLength + (meta.buildingUnitAmount.y - 1) * meta.buildingUnitInterval + meta.mineRadius);
            BuildingModeManager.Instance.InstantiateMine(minePos);
        }
        public void RestartAll()
        {
            SceneManager.LoadScene(0);
        }
        //一键整体预览，每个地块随机到某个等级的房产，先模拟开垦地块，然后盖上房子，然后开始生产
        public void WholeProcessPreview()
        {
            foreach (var bu in buildUnitArray)
            {
                if(bu != states.baseBuildUnit)
                {
                    StartCoroutine(OneProcessPreview(bu));
                }
            }
        }
        private IEnumerator OneProcessPreview(BuildUnit bu)
        {
            int level = Random.Range(0, meta.buildingLevels);
            yield return wait1;
            for (int i = 0; i <= meta.GetBuildingTookupPlace(level); i++)
            {
                bu.SetGridOpenProgress(i);
                yield return wait1;
            }
            bu.PlaceBuilding(new BuildingDataStruct(Random.Range(0,meta.buildingMetaData.TypeCount), level));
            SetProduceOnBuildUnit(bu);
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(4, 8));
                SetProduceOnBuildUnit(bu);
            }
        }
        //寻路，传入起点终点，返回从起点到终点的路径点按顺序排列的集合
        private List<PathFindingNode> FindPath(PathFindingNode start, PathFindingNode end)
        {
            List<PathFindingNode> path = new List<PathFindingNode>();
            path.Add(start);
            PathFindingNode pathInLoop = start;
            while (!end.Equals(pathInLoop))
            {
                pathInLoop = pathInLoop.NextShortestNodeToTarget(end);
                path.Add(pathInLoop);
            }
            //测试结果
            //foreach (var item in path)
            //{
            //    Instantiate(testPeoplePrefab, item.worldPos, Quaternion.identity);
            //}
            return path;
        }

        //根据（当前）所选建筑生成角色，需要角色种类（默认和建筑种类一一对应，拿建筑种类就行），并移动
        public void SpawnWorker(BuildUnit bu,List<PathFindingNode> path)
        {
            GameObject worker = bu.GetWorker();
            WorkerAutoMovement wam = worker.GetComponent<WorkerAutoMovement>();
            wam.SetPathToStart(path);
        }

        private void BeforeCurrentSelectedBuildUnitChange(BuildUnit before,BuildUnit after)
        {
            if(before != null && after == null)
            {
                UIManager.Instance.HideManagePanel();
            }
            else
            {
                if(after != null)
                {
                    UpdateUIState(after);
                    if(before == null)
                    {
                        UIManager.Instance.ShowManagePanel(after);
                    }
                }
            }
        }
        //更新UI界面状态
        public void UpdateUIState()
        {
            BuildUnit bu = states.currentSelectBuildUnit.Value;
            UpdateUIState(bu);
        }
        public void UpdateUIState(BuildUnit bu)
        {
            if(bu != null)
            {
                UIPopPanelState upps = new UIPopPanelState();
                upps.canOpenGrid = bu.CanOpenGrid && states.playerState.Value == (int)PlayerState.Idle;
                upps.canBuild = bu.CanBuild;
                upps.canExtend = bu.CanExtend;
                upps.canProduce = bu.CanProduce && (states.playerState.Value == (int)PlayerState.Idle || states.playerState.Value == (int)PlayerState.Busy);
                upps.canSellGrid = bu.CanSellGrid;
                upps.canSellBuilding = bu.CanSellBuilding;
                upps.canCheckProgress = bu.CanCheckProgress;
                states.uiPopPanelState.Value = upps;
            }
        }
        //--------------------------主要事件----------------------------
        //开垦
        public void OpenGridOnBuildUnit()
        {
            states.currentSelectBuildUnit.Value.OpenOneGrid();
            UpdateUIState();
        }
        //建造
        public void BuildStructureOnBuildUnit(BuildingDataStruct bds)
        {
            states.currentSelectBuildUnit.Value.PlaceBuilding(bds);
            UpdateUIState();
        }
        //扩建
        public void ExtendStructureOnBuildUnit()
        {
            states.currentSelectBuildUnit.Value.ExtendBuilding();
            UpdateUIState();
        }
        //生产
        public void SetProduceOnBuildUnit()
        {
            SetProduceOnBuildUnit(states.currentSelectBuildUnit.Value);
        }
        public void SetProduceOnBuildUnit(BuildUnit bu)
        {
            bu.SetProduce();
            UpdateUIState();
            //先寻路，生成小人沿路走，走到地方再设置生产
            List<PathFindingNode> path = FindPath(states.baseBuildUnit.GetPathFindingPoint(), bu.GetPathFindingPoint());
            SpawnWorker(bu,path);
        }
        //查看
        public void CheckProgressOnBuildUnit()
        {
            states.currentSelectBuildUnit.Value.CheckProduceProgress();
            UpdateUIState();
        }
        //出售地块
        public void SellGridOnBuildUnit()
        {
            states.currentSelectBuildUnit.Value.SellGrid();
            UpdateUIState();
        }
        //出售房产
        public void SellBuildingOnBuildUnit()
        {
            states.currentSelectBuildUnit.Value.SellBuilding();
            UpdateUIState();
        }

        #endregion

        #region 模式切换
        private void BeforeGameStateChang(int before, int after)
        {
            GameState s1 = (GameState)before;
            GameState s2 = (GameState)after;
            if (s1 != GameState.None)
            {
                states.state_modeManager_dic[s1].ExitMode();
            }
            if (s2 != GameState.None)
            {
                states.state_modeManager_dic[s2].EnterMode();
            }
        }
        public void SwitchModes(GameState changeToState)
        {
            int toState = (int)changeToState;
            if (states.gameState.Value == toState)
            {
                states.gameState.Value = (int)GameState.None;
            }
            else
            {
                states.gameState.Value = toState;
            }
        }
        #endregion

        #region 网格 选择、取消选择

        public void SelectOneGrid()
        {
            //只允许在 非null 变化时赋值，null直接赋值
            GridObject go = DetectGrid();
            if (go != null && !go.Equals(states.currentSelectGrid.Value))
            {
                states.currentSelectGrid.Value = go;
            }
            else if (go == null)
            {
                states.currentSelectGrid.Value = null;
            }
        }

        //射线检测碰撞的网格
        public GridObject DetectGrid()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hitinfo, Mathf.Infinity, 1 << ConstAndStatics.gridCellLayer))
            {
                return GetGridByWorldPos(hitinfo.point);
            }
            return null;
        }
        public BuildUnit DetectBuildUnit()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hitinfo, Mathf.Infinity, 1 << ConstAndStatics.gridCellLayer))
            {
                return GetBuildUnitByWorldPos(hitinfo.point);
            }
            return null;
        }
        #endregion

        #region 网格 创建、查询、修改
        public void InitGrids()
        {
            //初始化地块数组
            buildUnitArray = new BuildUnit[meta.buildingUnitAmount.x, meta.buildingUnitAmount.y];
            for (int i = 0; i < meta.buildingUnitAmount.x; i++)
            {
                for (int j = 0; j < meta.buildingUnitAmount.y; j++)
                {
                    buildUnitArray[i, j] = new BuildUnit(new Vector2Int(i,j));
                }
            }
            //Grid数组
            gridArray = new GridObject[meta.gridSize.x, meta.gridSize.y];
            for (int i = 0; i < meta.gridSize.x; i++)
            {
                for (int j = 0; j < meta.gridSize.y; j++)
                {
                    gridArray[i, j] = CreateGrid(i, j);
                }
            }
            //地块装填完毕初始哈
            foreach (var item in buildUnitArray)
            {
                item.Init();
            }
        }
        private GridObject CreateGrid(int x, int z)
        {
            GridObject obj = Instantiate(meta.gridPrefab, this.transform);
            Vector2Int buildUnitCoordinate = CalculateBuildUnitByGridPos(x, z);
            buildUnitArray[buildUnitCoordinate.x, buildUnitCoordinate.y].SetGridObject(obj);
            obj.Init(new Vector2Int(x,z), buildUnitArray[buildUnitCoordinate.x, buildUnitCoordinate.y]);
            //地块设置、偏移
            Vector3 buildUnitIntervalOffset = new Vector3(buildUnitCoordinate.x, 0.0f, buildUnitCoordinate.y) * meta.buildingUnitInterval;
            obj.transform.localPosition = new Vector3(meta.gridUnitLength * x, 0.0f, meta.gridUnitLength * z) + buildUnitIntervalOffset;
            if (isDebug)
            {
                DebugUtils.CreateWorldText("(" + x + "," + z + ")", obj.transform);
            }
            return obj;
        }
        private Vector2Int CalculateBuildUnitByGridPos(int x,int z)
        {
            return new Vector2Int(x / meta.buildingUnitSize.x, z / meta.buildingUnitSize.y);
        }
        public GridObject GetGridByWorldPos(Vector3 pos)
        {
            Vector4Int newpos = GetIndexByWorldPos(pos);
            return GetGridByIndex(newpos.z, newpos.w);
        }
        public BuildUnit GetBuildUnitByWorldPos(Vector3 pos)
        {
            Vector4Int newpos = GetIndexByWorldPos(pos);
            return GetBuildUnitByIndex(newpos.x, newpos.y);
        }
        public GridObject GetGridByIndex(int x, int z)
        {
            if (ValidGridIndex(x, z))
            {
                return gridArray[x, z];
            }
            return null;
        }
        public BuildUnit GetBuildUnitByIndex(int x, int z)
        {
            if (ValidBuildUnitIndex(x, z))
            {
                return buildUnitArray[x, z];
            }
            return null;
        }

        //通过世界坐标获取Grid，不需要判断是否点击到了路面，因为只有点击到Grid才会通过射线检测
        //带过道的检测方法：先检测出所属的坑位，横纵剪掉被增加进去的过道长度，然后依旧按照密集排布方式计算
        //返回Vecor4Int类型 ，前两位是坑位坐标，后两位是Grid坐标
        private Vector4Int GetIndexByWorldPos(Vector3 pos)
        {
            pos = transform.InverseTransformPoint(pos);
            int buildUnitX = (int)Mathf.Floor((pos.x + 0.5f * meta.gridUnitLength) / (meta.buildingUnitSize.x + meta.buildingUnitInterval));
            int buildUnitY = (int)Mathf.Floor((pos.z + 0.5f * meta.gridUnitLength) / (meta.buildingUnitSize.y + meta.buildingUnitInterval));
            //Debug.Log("所属大区为：" + buildUnitX + "   " + buildUnitY);
            int x = (int)Mathf.Floor((pos.x + 0.5f * meta.gridUnitLength - buildUnitX*meta.buildingUnitInterval) / meta.gridUnitLength);
            int z = (int)Mathf.Floor((pos.z + 0.5f * meta.gridUnitLength - buildUnitY*meta.buildingUnitInterval) / meta.gridUnitLength);
            //Debug.Log("最终结果为：" + x + "   " + z);
            return new Vector4Int(buildUnitX, buildUnitY,x,z);
        }
        private bool ValidGridIndex(int x, int z)
        {
            return x >= 0 && x < meta.gridSize.x && z >= 0 && z < meta.gridSize.y;
        }
        private bool ValidBuildUnitIndex(int x, int z)
        {
            return x >= 0 && x < meta.buildingUnitAmount.x && z >= 0 && z < meta.buildingUnitAmount.y;
        }
        #endregion
    }
}