using System;
using System.Collections.Generic;
using TEngine;
using UnityEngine;

/// <summary>
/// 游戏模块
/// </summary>
public class GameModule:MonoBehaviour
{
    #region BaseComponents
    /// <summary>
    /// 获取游戏基础组件。
    /// </summary>
    public static RootComponent Base { get; private set; }

    /// <summary>
    /// 获取调试组件。
    /// </summary>
    public static DebuggerComponent Debugger { get; private set; }

    /// <summary>
    /// 获取有限状态机组件。
    /// </summary>
    public static FsmComponent Fsm { get; private set; }

    /// <summary>
    /// 获取对象池组件。
    /// </summary>
    public static ObjectPoolComponent ObjectPool { get; private set; }

    /// <summary>
    /// 获取资源组件。
    /// </summary>
    public static ResourceComponent Resource { get; private set; }

    /// <summary>
    /// 获取配置组件。
    /// </summary>
    public static SettingComponent Setting { get; private set; }

    /// <summary>
    /// 获取界面组件。
    /// </summary>
    public static UIComponent UI { get; private set; }

    #endregion

    /// <summary>
    /// 初始化系统框架模块
    /// </summary>
    public static void InitFrameWorkComponents()
    {
        Base = Get<RootComponent>();
        Debugger = Get<DebuggerComponent>();
        Fsm = Get<FsmComponent>();
        ObjectPool = Get<ObjectPoolComponent>();
        Resource = Get<ResourceComponent>();
        Setting = Get<SettingComponent>();
        UI = Get<UIComponent>();
    }

    public static void InitCustomComponents()
    {
        
    }
  
    private static readonly Dictionary<Type, GameFrameworkComponent> s_Components = new Dictionary<Type, GameFrameworkComponent>();

    public static T Get<T>()where T : GameFrameworkComponent
    {
        Type type = typeof(T);
        
        if (s_Components.ContainsKey(type))
        {
            return s_Components[type] as T;
        }
        
        T component = TEngine.GameEntry.GetComponent<T>();
        
        Log.Assert(condition:component != null,$"{typeof(T)} is null");
        
        s_Components.Add(type,component);

        return component;
    }

    public void Start()
    {
        Log.Info("GameModule Active");
        InitFrameWorkComponents();
        InitCustomComponents();
    }
}