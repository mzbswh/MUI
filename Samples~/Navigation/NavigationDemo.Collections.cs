using System;
using System.Linq;
using UnityEngine;

namespace MUI.Samples.Navigation
{
    public sealed partial class NavigationDemo
    {
        private static void DemonstrateCollections()
        {
            var items = new ObservableList<string>(new[] { "wood", "stone" }, itemKey: value => value);
            var notifications = 0;
            items.Changed += batch =>
            {
                notifications++;
                Debug.Log($"MUI List batch: version={batch.Version}; changes={string.Join(",", batch.Changes.Select(change => change.Kind))}; count={batch.Count}");
                try
                {
                    items.Add("reentrant");
                }
                catch (InvalidOperationException)
                {
                    Debug.Log("MUI List reentrant edit rejected");
                }
            };
            ObservableListEditor<string> expired = null;
            items.Edit(edit =>
            {
                expired = edit;
                edit.AddRange(new[] { "iron", "gold" });
                edit.Move(0, 3);
                edit.Replace(0, "silver");
                edit.NotifyUpdated(1);
                edit.RemoveAt(2);
            });
            Debug.Log($"MUI List committed: items={string.Join(",", items)}; notifications={notifications}");
            try
            {
                expired.Add("expired");
            }
            catch (InvalidOperationException)
            {
                Debug.Log("MUI List expired editor rejected");
            }
            try
            {
                items.Add("iron");
            }
            catch (InvalidOperationException) { Debug.Log($"MUI List duplicate rejected: version={items.Version}; count={items.Count}"); }
            try
            {
                items.Edit(edit =>
                {
                    edit.Clear();
                    throw new InvalidOperationException("Demonstrated edit failure.");
                });
            }
            catch (InvalidOperationException) { Debug.Log($"MUI List edit rollback: version={items.Version}; count={items.Count}"); }
            items.Reset(Enumerable.Range(0, 10000).Select(index => "item-" + index));
            Debug.Log($"MUI List bulk: count={items.Count}; notifications={notifications}; version={items.Version}");
        }
    }
}
