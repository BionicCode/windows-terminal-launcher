namespace BionicCode.Utilities.Net;

using System.Collections;
using System.Diagnostics.CodeAnalysis;

/// <summary>
/// A wrapper that decorates and underlying value and acts like the wrapped value by hiding it's own type in e.g. expressions and statements.
/// <br/><see cref="WriteOnce{TValue}"/> acts like a write-once field or property, allowing the underlying value to be set only once and then acts as immutable.
/// </summary>
/// <remarks>
/// <see cref="WriteOnce{TValue}"/> implements a write-once semantics for the underlying value, allowing it to be set only once and then acts as immutable.
/// <para/><see cref="WriteOnce{TValue}"/> additionally acts like a normal field or property 
/// in that it can be implicitly cast to and from the underlying value type, allowing it to be used in expressions 
/// and assignments without needing to explicitly access the underlying value.
/// <para/><see cref="WriteOnce{TValue}"/> is thread-safe, ensuring that the underlying value can only be set once even in concurrent scenarios.
/// <para/><see cref="WriteOnce{TValue}"/> implements <see cref="IFormattable"/> to allow for formatted string representations based on the underlying value.
/// <para>Typical usage is to wrap e.g. a OnCollectionChanged call with a using() scope or using expression:</para>
/// <code>
/// class Foo
/// {
///     private readonly WriteOnce&lt;bool&gt; _isInitialized;
///     private readonly WriteOnce&lt;string&gt; _name;
///     private readonly WriteOnce&lt;double&gt; _offset;
///     
///     private void Initialize(double offset)
///     {
///         _offset.SetValue(offset);
///         _isInitialized.SetValue(true);
///         
///         // Initializing a WriteOnce&lt;string&gt; with an implicit cast from string literal, 
///         // making it act like a plain string variable. It's equivalent to calling '_name.SetValue("Malcolm X")' 
///         // in terms of creating a new initialized and frozen/immutable instance.
///         WriteOnce&lt;string&gt; name = "Malcolm X";
///     }
///     
///     private void DoSomething()
///     {
///         // Implicit cast allows treating the WriteOnce&lt;bool&gt; like plain bool field.
///         // WriteOnce&lt;bool&gt; also behaves like a field as that it would return 'default(bool)' 
///         // if not explicitly initialized with a value.
///         if (_isInitialized)
///         {
///             // Implicit cast makes the WriteOnce&lt;string&gt; act like a plain string field.
///             double length = 24.476 + _offset;
///             
///             // If 'TValue' is IFormattable, the WriteOnce&lt;TValue&gt; can be formatted directly 
///             // without needing to access the underlying value.
///             Console.WriteLine($"SourceFileName: {_name}, Length: {length:F2}");
///         }
///     }
/// }
/// </code>
/// </remarks>
public sealed class WriteOnce<TValue> : IFormattable
{
    private TValue _value = default!;
    private int _isSet;
    private readonly object _syncLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WriteOnce{TValue}"/> class with the specified value and marks it as set (immutable).
    /// </summary>
    /// <remarks>Using this constructor initializes the <see cref="WriteOnce{TValue}"/> instance with the provided value and marks it as set, meaning that the value cannot be modified afterward.
    /// <param name="value">The value to initialize the <see cref="WriteOnce{TValue}"/> instance with.</param>
    public WriteOnce(TValue value)
    {
        _value = value;
        _isSet = 1;
    }

    public WriteOnce()
    {

    }

    public bool TrySetValue(TValue value)
    {
        if (Volatile.Read(ref _isSet) != 0)
        {
            return false;
        }

        lock (_syncLock)
        {
            if (_isSet != 0)
            {
                return false;
            }

            _value = value;
            Volatile.Write(ref _isSet, 1);
            return true;
        }
    }

    public TValue SetValue(TValue value) => TrySetValue(value) 
        ? this 
        : throw new InvalidOperationException("UserEnvironmentRegistryKey has already been initialized and cannot be modified.");

    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Required.")]
    public TValue GetValueOrDefault() => Volatile.Read(ref _isSet) != 0 ? _value : default!;

    /// <summary>
    /// Gets whether the current instance has been set with a value and is therefore sealed. 
    /// </summary>
    /// <remarks>Once a value has been set, the <see cref="WriteOnce{TValue}"/> instance is considered sealed and immutable. This property indicates whether the instance has been set with a value or not.</remarks>
    /// <value><see langword="true"/> if the instance has been set with a value and is sealed for further writes; otherwise, <see langword="false"/>.</value>
    public bool IsSet => Volatile.Read(ref _isSet) != 0;

    /// <summary>
    /// Returns a string representation of the current underlying value, or an empty string if no value is present.
    /// </summary>
    /// <returns>A string that represents the current underlying value if present; otherwise, an empty string.</returns>
    public override string ToString() => GetValueOrDefault() is TValue value
        ? value.ToString() ?? string.Empty
        : string.Empty;

    /// <summary>
    /// Performs an equality comparison between this instance and another <see cref="WriteOnce{TValue}"/> instance based on the underlying values AND NOT on their references.
    /// </summary>
    /// <remarks>Equality is based on the underlying values of the <see cref="WriteOnce{TValue}"/> instances.
    /// <br/>For reference equality, use the <see cref="ReferenceEquals(object, object)"/> method or call <see cref="object.Equals(object)"/>.
    /// <para/><see cref="object.GetHashCode"/> and <see cref="object.Equals"/> are based on the wrapping <see cref="WriteOnce{TValue}"/> reference equality.</remarks>
    /// <param name="other">The other <see cref="WriteOnce{TValue}"/> instance to compare with.</param>
    /// <returns><see langword="true"/> if the underlying values are equal; otherwise, <see langword="false"/>.</returns>
    public bool ValueEquals(WriteOnce<TValue>? other) => ValueEquals(this, other);

    /// <summary>
    /// Performs an equality comparison between this instance and another <see cref="WriteOnce{TValue}"/> instance based on the underlying values AND NOT on their references.
    /// </summary>
    /// <remarks>Equality is based on the underlying values of the <see cref="WriteOnce{TValue}"/> instances.
    /// <br/>For reference equality, use the <see cref="ReferenceEquals(object, object)"/> method or call <see cref="object.Equals(object)"/>.
    /// <para/><see cref="object.GetHashCode"/> and <see cref="object.Equals"/> are based on the wrapping <see cref="WriteOnce{TValue}"/> reference equality.</remarks>
    /// <param name="other">The other <see cref="WriteOnce{TValue}"/> instance to compare with.</param>
    /// <returns><see langword="true"/> if the underlying values are equal; otherwise, <see langword="false"/>.</returns>
    public bool ValueEquals(WriteOnce<TValue>? x, WriteOnce<TValue>? y) => ReferenceEquals(x, y)
        || (x is not null && y is not null && EqualityComparer<TValue>.Default.Equals(x.GetValueOrDefault(), y.GetValueOrDefault()));

    /// <summary>
    /// Returns the string representation of the current underlying value, using the specified format and culture-specific format
    /// information.
    /// </summary>
    /// <remarks>If the underlying value implements <see cref="IFormattable"/> like e.g. <see cref="int"/>, its <see cref="IFormattable.ToString"/> implementation is used.
    /// Otherwise, the default <see cref="object.ToString"/> method on the underlying value is called.</remarks>
    /// <param name="format">A standard or custom format string that defines the format to use. If null or empty, the default format is used.</param>
    /// <param name="formatProvider">An object that supplies culture-specific formatting information. If null, the current culture is used.</param>
    /// <returns>A string representation of the current object, formatted as specified by the format and formatProvider
    /// parameters.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider) => GetValueOrDefault() is IFormattable formattable
        ? formattable.ToString(format, formatProvider)
        : ToString();

    #region Operators
    public static implicit operator TValue(WriteOnce<TValue>? source) => source is null
        ? default!
        : source.GetValueOrDefault();

    public static implicit operator WriteOnce<TValue>(TValue source) => new(source);
    #endregion Operators
}