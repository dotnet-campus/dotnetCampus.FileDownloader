namespace dotnetCampus.FileDownloader
{
    /// <summary>
    /// Identifies a logging event. The primary identifier is the "Id" property, with the "Name" property providing a short description of this type of event.
    /// </summary>
    public readonly struct EventId
    {
        /// <summary>
        /// Implicitly creates an EventId from the given <see cref="T:System.Int32" />.
        /// </summary>
        /// <param name="i">The <see cref="T:System.Int32" /> to convert to an EventId.</param>
        public static implicit operator EventId(int i)
        {
            return new EventId(i, (string) null!);
        }

        /// <summary>
        /// Checks if two specified <see cref="T:Microsoft.Extensions.Logging.EventId" /> instances have the same value. They are equal if they have the same Id.
        /// </summary>
        /// <param name="left">The first <see cref="T:Microsoft.Extensions.Logging.EventId" />.</param>
        /// <param name="right">The second <see cref="T:Microsoft.Extensions.Logging.EventId" />.</param>
        /// <returns><code>true</code> if the objects are equal.</returns>
        public static bool operator ==(EventId left, EventId right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Checks if two specified <see cref="T:Microsoft.Extensions.Logging.EventId" /> instances have different values.
        /// </summary>
        /// <param name="left">The first <see cref="T:Microsoft.Extensions.Logging.EventId" />.</param>
        /// <param name="right">The second <see cref="T:Microsoft.Extensions.Logging.EventId" />.</param>
        /// <returns><code>true</code> if the objects are not equal.</returns>
        public static bool operator !=(EventId left, EventId right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Initializes an instance of the <see cref="T:Microsoft.Extensions.Logging.EventId" /> struct.
        /// </summary>
        /// <param name="id">The numeric identifier for this event.</param>
        /// <param name="name">The name of this event.</param>
        public EventId(int id, string name = null!)
        {
            this.Id = id;
            this.Name = name;
        }

        /// <summary>Gets the numeric identifier for this event.</summary>
        public int Id { get; }

        /// <summary>Gets the name of this event.</summary>
        public string Name { get; }

        /// <inheritdoc />
        public override string ToString()
        {
            return this.Name ?? this.Id.ToString();
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type. Two events are equal if they have the same id.
        /// </summary>
        /// <param name="other">An object to compare with this object.</param>
        /// <returns><code>true</code> if the current object is equal to the other parameter; otherwise, <code>false</code>.</returns>
        public bool Equals(EventId other)
        {
            return this.Id == other.Id;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj != null && obj is EventId other && this.Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return this.Id;
        }
    }
}
