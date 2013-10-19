﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RemoteTech
{
    public class AntennaFragment : IFragment, IDisposable
    {
        private class Entry
        {
            public String Text { get; set; }
            public Guid Guid { get; set; }
            public Color Color;
            public List<Entry> SubEntries { get; private set; }
            public bool Expanded { get; set; }
            public int Depth { get; set; }

            public Entry()
            {
                SubEntries = new List<Entry>();
                Expanded = true;
            }
        }

        private readonly IAntenna mAntenna;
        private Vector2 mScrollPosition = Vector2.zero;
        private Entry mRootEntry = new Entry();
        private Entry mSelection;
        private Action mOnQuit;

        public AntennaFragment(IAntenna antenna, Action quit)
        {
            mAntenna = antenna;
            mOnQuit = quit;
            RTCore.Instance.Satellites.OnRegister += Refresh;
            RTCore.Instance.Satellites.OnUnregister += Refresh;
            RTCore.Instance.Antennas.OnUnregister += Refresh;
            Refresh();
        }

        public void Dispose()
        {
            RTCore.Instance.Satellites.OnRegister -= Refresh;
            RTCore.Instance.Satellites.OnUnregister -= Refresh;
            RTCore.Instance.Antennas.OnUnregister -= Refresh;
        }

        public void Draw()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            {
                mScrollPosition = GUILayout.BeginScrollView(mScrollPosition);
                {
                    Color pushColor = GUI.backgroundColor;
                    TextAnchor pushAlign = GUI.skin.button.alignment;
                    GUI.skin.button.alignment = TextAnchor.MiddleLeft;
                    // Depth-first tree traversal.
                    Stack<Entry> dfs = new Stack<Entry>();
                    foreach (Entry child in mRootEntry.SubEntries)
                    {
                        dfs.Push(child);
                    }
                    while (dfs.Count > 0)
                    {
                        Entry current = dfs.Pop();
                        GUI.backgroundColor = current.Color;

                        GUILayout.BeginHorizontal();
                        {
                            GUILayout.Space(current.Depth * (GUI.skin.button.margin.left + 18));
                            if (current.SubEntries.Count > 0)
                            {
                                RTUtil.Button(current.Expanded ? "<" : ">",
                                    () =>
                                    {
                                        current.Expanded = !current.Expanded;
                                    }, GUILayout.Width(18));
                            }
                            RTUtil.StateButton(current.Text, mSelection == current ? 1 : 0, 1,
                                (s) =>
                                {
                                    mSelection = current;
                                    mAntenna.Target = mSelection.Guid;
                                });

                        }
                        GUILayout.EndHorizontal();

                        if (current.Expanded)
                        {
                            foreach (Entry child in current.SubEntries)
                            {
                                dfs.Push(child);
                            }
                        }
                    }

                    GUI.skin.button.alignment = pushAlign;
                    GUI.backgroundColor = pushColor;
                }
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        public void Refresh(IAntenna sat) { if (sat == mAntenna) mOnQuit.Invoke(); }
        public void Refresh(ISatellite sat) { Refresh(); }
        public void Refresh()
        {
            Dictionary<CelestialBody, Entry> mEntries = new Dictionary<CelestialBody, Entry>();

            mRootEntry = new Entry();
            mSelection = new Entry()
            {
                Text = "No Target",
                Guid = Guid.Empty,
                Color = Color.white,
            };
            mRootEntry.SubEntries.Add(mSelection);

            // Add the planets
            foreach (var cb in RTCore.Instance.Network.Planets)
            {
                if (!mEntries.ContainsKey(cb.Value))
                {
                    mEntries[cb.Value] = new Entry();
                }

                Entry current = mEntries[cb.Value];
                current.Text = cb.Value.bodyName;
                current.Guid = cb.Key;
                current.Color = cb.Value.GetOrbitDriver() != null
                    ? cb.Value.GetOrbitDriver().Renderer.orbitColor : Color.yellow;
                current.Color.a = 1.0f;

                if (cb.Value.referenceBody != cb.Value)
                {
                    CelestialBody parent = cb.Value.referenceBody;
                    if (!mEntries.ContainsKey(parent))
                    {
                        mEntries[parent] = new Entry();
                    }
                    mEntries[parent].SubEntries.Add(current);
                }
                else
                {
                    mRootEntry.SubEntries.Add(current);
                }

                if (cb.Key == mAntenna.Target)
                {
                    mSelection = current;
                }
            }

            // Sort the lists based on semi-major axis. In reverse because of how we render it.
            foreach (var entryPair in mEntries)
            {
                entryPair.Value.SubEntries.Sort((b, a) =>
                {
                    return RTCore.Instance.Network.Planets[a.Guid].orbit.semiMajorAxis.CompareTo(
                           RTCore.Instance.Network.Planets[b.Guid].orbit.semiMajorAxis);
                });
            }

            // Add the satellites.
            foreach (ISatellite s in RTCore.Instance.Network)
            {
                Entry current = new Entry()
                {
                    Text = s.Name,
                    Guid = s.Guid,
                    Color = Color.white,
                };
                mEntries[s.Body].SubEntries.Add(current);

                if (s.Guid == mAntenna.Target)
                {
                    mSelection = current;
                }
            }

            // Set a local depth variable so we can refer to it when rendering.
            mRootEntry.SubEntries.Reverse();
            Stack<Entry> dfs = new Stack<Entry>();
            foreach (Entry child in mRootEntry.SubEntries)
            {
                child.Depth = 0;
                dfs.Push(child);
            }
            while (dfs.Count > 0)
            {
                Entry current = dfs.Pop();
                foreach (Entry child in current.SubEntries)
                {
                    child.Depth = current.Depth + 1;
                    dfs.Push(child);
                }
            }
        }
    }
}
