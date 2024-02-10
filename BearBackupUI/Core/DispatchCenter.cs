namespace BearBackupUI.Core;

public class DispatchCenter
{
    private readonly List<Type> _sections;
    private readonly List<(EventHandler<ActionArgs>, int[]?)> _listeners;

    public DispatchCenter()
    {
        _sections = [];
        _listeners = [];
    }

    public void AddListener(EventHandler<ActionArgs> listener)
    {
        _listeners.Add((listener, null));
    }

    public void AddListener(Type section, EventHandler<ActionArgs> listener)
    {
        AddListener([section], listener);
    }

    public void AddListener(Type[] sections, EventHandler<ActionArgs> listener)
    {
        var indexes = new List<int>();
        foreach (var section in sections)
        {
            var index = _sections.IndexOf(section);
            if (index == -1)
            {
                _sections.Add(section);
                index = _sections.Count - 1;
            }
            indexes.Add(index);
        }

        _listeners.Add((listener, indexes.ToArray()));
    }

    public void RemoveListener(EventHandler<ActionArgs> listener)
    {
        var removes = _listeners.FindAll(i => i.Item1 == listener);

        foreach (var remove in removes)
        {
            _listeners.Remove(remove);
        }
    }

    public void DispatchEvent(ActionArgs actionArgs, bool newThread = false)
    {
        if (newThread)
            Task.Run(() => DispatchEventBase(actionArgs));
        else
            DispatchEventBase(actionArgs);
    }

    private void DispatchEventBase(ActionArgs actionArgs)
    {
        var secIndex = _sections.IndexOf(actionArgs.Type.GetType());

        if (secIndex == -1)
        {
            foreach (var listener in _listeners.ToArray())
            {
                if (listener.Item2 != null) continue;

                listener.Item1.Invoke(this, actionArgs);
            }
        }
        else
        {
            foreach (var listener in _listeners.ToArray())
            {
                if (listener.Item2 != null)
                {
                    if (!listener.Item2.Contains(secIndex)) continue;
                }

                listener.Item1.Invoke(this, actionArgs);
            }
        }
    }

    public Type[] GetSections()
    {
        return [.. _sections];
    }
}
