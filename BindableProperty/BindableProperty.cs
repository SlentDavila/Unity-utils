using System;

//特性：赋值时，与原值相同不触发任何方法、不重新赋值。 与原值不同，赋值前调用一次委托，复制后调用另一个委托。
//主要用于数据驱动逻辑，比如游戏模式变化  开始、暂停、结束等flag变化时赋值前后有连带关系的逻辑，用该方式防止某些地方改动了数据后忘了调用逻辑，或调用某些逻辑忘了改动flag，此方法只需要修改flag自动触发连携方法，代码会更加简洁。
//并且游戏数据更加可视化，可以通过检查面板上的value值查看游戏状态，调试更方便
public class BindableProperty<T>
{
    private T mValue = default(T);
    public T Value
    {
        get
        {
            return mValue;
        }
        set
        {
            if (!value.Equals(mValue))
            {
                beforeValueChanged?.Invoke(mValue, value);
                mValue = value;
                onValueChanged?.Invoke(value);
            }
        }
    }

    public Action<T> onValueChanged; //指定回调方法，回调方法中会把他自己传进去
    public Action<T,T> beforeValueChanged; //第一个参数是原数据，第二个参数是新数据

}
