using System;

namespace MUI.Resources
{
    public sealed class ViewResource : IEquatable<ViewResource>
    {
        public ViewResource(string key, string version = "1")
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("View resource key is required.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentException("Resource version is required.", nameof(version));
            }

            Key = key;
            Version = version;
        }

        public string Key
        {
            get;
        }

        public string Version
        {
            get;
        }

        public bool Equals(ViewResource other) => other != null && Key == other.Key && Version == other.Version;

        public override bool Equals(object obj) => obj is ViewResource other && Equals(other);

        public override int GetHashCode() => unchecked(Key.GetHashCode() * 397 ^ Version.GetHashCode());

        public override string ToString() => Key + "@" + Version;
    }
}
