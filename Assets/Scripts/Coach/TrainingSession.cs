using System;
using System.Collections.Generic;

/// <summary>
/// Plain C# data model that is the single source of truth for the Training Flow.
/// The UI (CoachDashboardUIToolkitController) is a view that observes the two events
/// and reflects state changes (ListView contents, repText, active-row highlight).
/// The <see cref="Drills"/> list is used directly as the ListView's itemsSource, so
/// the built-in reorder mutates it in place; callers fix up <see cref="ActiveIndex"/>
/// via <see cref="OnReordered"/>.
/// </summary>
public class TrainingSession
{
    /// <summary>Ordered list of drill names. Also assigned as the ListView itemsSource.</summary>
    public readonly List<string> Drills = new List<string>();

    /// <summary>Index of the active drill, or -1 when none is active.</summary>
    public int ActiveIndex { get; private set; } = -1;

    /// <summary>Raised when drills are added, removed, or reordered (count or order change).</summary>
    public event Action OnDrillsChanged;

    /// <summary>Raised when the active drill index changes.</summary>
    public event Action OnActiveDrillChanged;

    /// <summary>The currently active drill name, or null when none is active.</summary>
    public string ActiveDrill =>
        (ActiveIndex >= 0 && ActiveIndex < Drills.Count) ? Drills[ActiveIndex] : null;

    public void AddDrill(string name)
    {
        Drills.Add(name);
        OnDrillsChanged?.Invoke();
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= Drills.Count) return;

        Drills.RemoveAt(index);

        // Keep ActiveIndex pointing at a sensible drill.
        bool activeChanged = false;
        if (Drills.Count == 0)
        {
            ActiveIndex = -1;
            activeChanged = true;
        }
        else if (index < ActiveIndex)
        {
            ActiveIndex--;
            activeChanged = true;
        }
        else if (index == ActiveIndex)
        {
            // The active drill itself was removed; clamp to the new last valid index.
            ActiveIndex = Math.Min(ActiveIndex, Drills.Count - 1);
            activeChanged = true;
        }

        OnDrillsChanged?.Invoke();
        if (activeChanged) OnActiveDrillChanged?.Invoke();
    }

    /// <summary>
    /// Called from ListView.itemIndexChanged AFTER the ListView already reordered the
    /// backing list. Only fixes up the active index so the highlight follows its drill.
    /// </summary>
    public void OnReordered(int oldIndex, int newIndex)
    {
        if (ActiveIndex == oldIndex) ActiveIndex = newIndex;
        else if (oldIndex < ActiveIndex && newIndex >= ActiveIndex) ActiveIndex--;
        else if (oldIndex > ActiveIndex && newIndex <= ActiveIndex) ActiveIndex++;

        OnDrillsChanged?.Invoke();
        OnActiveDrillChanged?.Invoke();
    }

    /// <summary>Advance to the next drill, wrapping to the first after the last.</summary>
    public void Next()
    {
        if (Drills.Count == 0) return;
        ActiveIndex = (ActiveIndex + 1) % Drills.Count;
        OnActiveDrillChanged?.Invoke();
    }

    /// <summary>Begin the session at the first drill (if any).</summary>
    public void Start()
    {
        if (Drills.Count > 0)
        {
            ActiveIndex = 0;
            OnActiveDrillChanged?.Invoke();
        }
    }

    /// <summary>Stop the session; no drill is active.</summary>
    public void Reset()
    {
        ActiveIndex = -1;
        OnActiveDrillChanged?.Invoke();
    }

    /// <summary>FORCE action: jump the active drill to the last drill in the flow.</summary>
    public void ForceToEnd()
    {
        if (Drills.Count == 0) return;
        ActiveIndex = Drills.Count - 1;
        OnActiveDrillChanged?.Invoke();
    }
}
