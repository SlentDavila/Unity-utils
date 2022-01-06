#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using Sirenix.Utilities;
using System.IO;
using LitJson;

namespace ArchiveUtility
{
    public class BackUpSaveWindow : OdinMenuEditorWindow
    {
        [MenuItem("ZGame/存档备份与恢复")]
        private static void Open()
        {
            var window = GetWindow<BackUpSaveWindow>();
            window.position = GUIHelper.GetEditorWindowRect().AlignCenter(1000, 800);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            OdinMenuTree tree = new OdinMenuTree(true);
            tree.Add("存档备份", new ScarlatArchive());
            // tree.AddAssetAtPath("音乐预载管理", "Assets/Resources/NaturePackage/ScriptableObjs/musicManage1.asset");
            tree.AddAllAssetsAtPath("元数据管理","Assets/Resources/NaturePackage/ScriptableObjs",typeof(ScriptableObject),true);
            return tree;
        }
    }
    //存档右侧面板，初始化时现读取存档备份、时间等信息
    public class ScarlatArchive
    {
        private SubArchiveShow baseSave;
        private string baseSaveInfo;
        [InfoBox("操作说明：标题显示当前存档包含哪些场景。\n 删除按钮会删除备份，覆盖按钮会复制当前存档覆盖备份\n读取按钮会读取备份覆盖至当前存档\n备份到新位置会创建新文件夹并保存备份")]
        [Title("$baseSaveInfo",TitleAlignment =TitleAlignments.Centered)]
        [TableList(AlwaysExpanded = true, IsReadOnly = true)]
        [LabelText("备份存档列表")]
        [ShowInInspector]
        public Queue<SubArchiveShow> subArcs = new Queue<SubArchiveShow>();

        [Button("备份当前存档到新位置（随机数字命名）", buttonSize: ButtonSizes.Large)]
        private void AddNewBackUp()
        {
            string newPath = recuirtFindValablePath(backSavePathRoot, subArcs.Count);
            DirectoryInfo dirinfo = new DirectoryInfo(curSavePath);
            FileInfo[] fileInfos = dirinfo.GetFiles();
            foreach (var item in fileInfos)
            {
                File.Copy(item.FullName, newPath + item.Name);
            }
            Init();
        }
        [HorizontalGroup("命名",500)]
        [Multiline(2)]
        [LabelText("新存档文件夹名称")]
        public string newSaveName;
        [HorizontalGroup("命名")]
        [Button("备份当前存档到新位置（使用自定义命名）",buttonSize:ButtonSizes.Large)]
        private void AddNewBackUpWithName()
        {
            if (string.IsNullOrEmpty(newSaveName))
            {
                EditorUtility.DisplayDialog("未命名", "未命名", "确定");
                return;
            }
            if(Directory.Exists(backSavePathRoot + newSaveName))
            {
                EditorUtility.DisplayDialog("文件夹已存在", "文件夹已存在", "确定");
                return;
            }
            Directory.CreateDirectory(backSavePathRoot + newSaveName);
            string newPath = backSavePathRoot + newSaveName+"/";
            DirectoryInfo dirinfo = new DirectoryInfo(curSavePath);
            FileInfo[] fileInfos = dirinfo.GetFiles();
            foreach (var item in fileInfos)
            {
                File.Copy(item.FullName, newPath + item.Name);
            }
            Init();
        }
        [Button("从其他文件夹复制一份备份",buttonSize:ButtonSizes.Large)]
        private void AddFromFolder()
        {
            string folderName = EditorUtility.OpenFolderPanel("要复制的文件夹", Application.dataPath, "");
            if (!string.IsNullOrEmpty(folderName))
            {
                string[] strs = folderName.Split('/');
                string name = strs[strs.Length - 1];

                string newPath = backSavePathRoot + name;
                if (Directory.Exists(newPath))
                {
                    if(EditorUtility.DisplayDialog("对应文件夹名称已存在", "对应文件夹名称已存在，是否覆盖文件夹", "确定", "取消"))
                    {
                        foreach (var item in new DirectoryInfo(newPath).GetFiles())
                        {
                            File.Delete(item.FullName);
                        }
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    Directory.CreateDirectory(newPath);
                }
                newPath += "/";
                //创建新文件夹、再复制、再读取、刷新
                foreach (var item in new DirectoryInfo(folderName).GetFiles())
                {
                    File.Copy(item.FullName, newPath + item.Name);
                }
                Init();
            }
        }

        private string recuirtFindValablePath(string basepath,int index)
        {
            if (Directory.Exists(basepath + index))
            {
                return recuirtFindValablePath(basepath, index + 1);
            }
            else
            {
                string avalablepath = basepath + index + "/";
                Directory.CreateDirectory(avalablepath);
                return avalablepath;
            }
        }

        private string curSavePath = Application.dataPath + "Caches";
        private string backSavePathRoot = Application.dataPath + "Caches/";
        //包含当前存档和备份中存档
        //备份存档文件夹格式：索引为目录名1 2等
        public ScarlatArchive()
        {
            Init();
        }

        private void Init()
        {
            subArcs.Clear();

            //读取正常存档
            DirectoryInfo dirinfo = new DirectoryInfo(curSavePath);
            try
            {
                ReadAndAdd(dirinfo);
                baseSave = subArcs.Dequeue();
                baseSaveInfo = "当前存档包含场景 ：" + baseSave.includeScenes.Replace("\n", "");
            }
            catch(Exception e)
            {
                Debug.LogError("窗口打开失败，通常是因为存档文件夹里还没有任何存档文件");
                Debug.LogError(e);
                return;
            }
            //读取备份存档
            foreach (var subinfo in dirinfo.GetDirectories())
            {
                ReadAndAdd(subinfo);
            }
        }
        private void DeleteAction(string pathName)
        {
            foreach (var item in new DirectoryInfo(pathName).GetFiles())
            {
                File.Delete(item.FullName);
            }
            Directory.Delete(pathName);
            Init();
            EditorUtility.DisplayDialog("已删除", "已删除" , "确定");
        }
        private void CopyAction(string path)
        {
            foreach (var item in new DirectoryInfo(path).GetFiles())
            {
                File.Delete(item.FullName);
            }
            foreach (var item in new DirectoryInfo(curSavePath).GetFiles())
            {
                File.Copy(item.FullName, path+"/" + item.Name);
            }
            Init();
            EditorUtility.DisplayDialog("已复制", "已复制", "确定");
        }
       
        private void ReadAction(string path)
        {
            foreach (var item in new DirectoryInfo(curSavePath).GetFiles())
            {
                File.Delete(item.FullName);
            }
            foreach (var item in new DirectoryInfo(path).GetFiles())
            {
                File.Copy(item.FullName, backSavePathRoot + item.Name);
            }
            Init();
            EditorUtility.DisplayDialog("已读取", "已读取", "确定");
        }

        private void ReadAndAdd(DirectoryInfo dirinfo)
        {
            FileInfo[] fileInfos = dirinfo.GetFiles();
            DateTime latestTime = fileInfos[0].LastWriteTime;
            foreach (var file in fileInfos)
            {
                if (file.LastWriteTime > latestTime) latestTime = file.LastWriteTime;
            }
            subArcs.Enqueue(new SubArchiveShow(dirinfo.FullName, latestTime.ToString("yyyy-MM-dd HH:mm:ss:ms"), ReadArchiveScenes(dirinfo.FullName), subArcs.Count, DeleteAction,CopyAction,ReadAction));
        }

         private string ReadArchiveScenes(string savepath)
        {
            string zhuijia = "";
            if (File.Exists(savepath + "/default.txt"))
            {
                zhuijia += " 默认：场景" + JsonMapper.ToObject<ArchiveData>(File.ReadAllText(savepath + "/default.txt")).sceneId + "\n";
            }
            if (File.Exists(savepath + "/archive1.txt"))
            {
                zhuijia += " slot1 : 场景" + JsonMapper.ToObject<ArchiveData>(File.ReadAllText(savepath + "/archive1.txt")).sceneId + "\n";
            }
            if (File.Exists(savepath + "/archive2.txt"))
            {
                zhuijia += " slot2 : 场景" + JsonMapper.ToObject<ArchiveData>(File.ReadAllText(savepath + "/archive2.txt")).sceneId + "\n";
            }
            if (File.Exists(savepath + "/archive3.txt"))
            {
                zhuijia += " slot3 : 场景" + JsonMapper.ToObject<ArchiveData>(File.ReadAllText(savepath + "/archive3.txt")).sceneId + "\n";
            }
            if (zhuijia.Equals(""))
            {
                zhuijia = "不含场景";
            }
            return zhuijia;
        }
    }

    public class SubArchiveShow
    {
        [DisplayAsString(false)]
        [HideLabel]
        [VerticalGroup("存档位置")]
        [NameOnTop]
        public string name;
        [DisplayAsString]
        [VerticalGroup("存档时间")]
        [HideLabel]
        public string curSaveString;
        [VerticalGroup("包含场景")]
        [HideLabel]
        [MultiLineProperty(4)]
        [ReadOnly]
        [EnableGUI]
        public string includeScenes;
        [HideInInspector]
        public int index;
        private Action<string> deleteAction;
        private Action<string> copyAction;
        private Action<string> readAction;

        public SubArchiveShow(string name, string curSaveString, string includeScenes,int index,Action<string> deleteAction, Action<string> copyAction, Action<string> readAction)
        {
            this.name = name;
            this.curSaveString = curSaveString;
            this.includeScenes = includeScenes;
            this.deleteAction = deleteAction;
            this.copyAction = copyAction;
            this.readAction = readAction;
        }

        [VerticalGroup("操作")]
        [Button("读取备份", buttonSize: ButtonSizes.Large)]
        [GUIColor(0.1f, 1.0f, 0.1f)]
        public void Read()
        {
            readAction.Invoke(name);
        }
        [HorizontalGroup("操作/二级按钮")]
        [Button("覆盖此备份", buttonSize: ButtonSizes.Large)]
        [GUIColor(0.1f, 0.5f, 1.0f)]
        public void CopyToThis()
        {
            copyAction.Invoke(name);
        }
        [HorizontalGroup("操作/二级按钮")]
        [Button("删除此备份", buttonSize: ButtonSizes.Large)]
        [GUIColor(1.0f, 0.1f, 0.1f)]
        public void Delete()
        {
            deleteAction.Invoke(name);
        }

    }
    public class NameOnTopAttribute : Attribute
    {
        
    }

    public class NameOnTopAttributeDrawer : OdinAttributeDrawer<NameOnTopAttribute, string>
    {
        protected override void DrawPropertyLayout(GUIContent label)
        {
            var rect = EditorGUILayout.GetControlRect();
            string[] strs = this.ValueEntry.SmartValue.Split('\\');
            EditorGUI.TextField(rect, "  " + strs[strs.Length - 1]);
            this.CallNextDrawer(label);
        }
    }
}

#endif
