using System.Runtime.InteropServices;

namespace DotNext;

using Runtime.CompilerServices;

/// <summary>
/// Represents functional interface returning arbitrary value and
/// accepting the single argument.
/// </summary>
/// <remarks>
/// Functional interface can be used to pass
/// some application logic without heap allocation in
/// contrast to regulat delegates. Additionally, implementation
/// of functional interface may have encapsulated data acting
/// as closure which is not allocated on the heap.
/// </remarks>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
public interface ISupplier<in T, out TResult> : IFunctional<Func<T, TResult>>
{
    /// <summary>
    /// Invokes the supplier.
    /// </summary>
    /// <param name="arg">The first argument.</param>
    /// <returns>The value returned by this supplier.</returns>
    TResult Invoke(T arg);

    /// <inheritdoc />
    Func<T, TResult> IFunctional<Func<T, TResult>>.ToDelegate() => Invoke;
}

/// <summary>
/// Represents typed function pointer implementing <see cref="ISupplier{T, TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct Supplier<T, TResult> : ISupplier<T, TResult>
{
    private readonly delegate*<T, TResult> ptr;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public Supplier(delegate*<T, TResult> ptr)
        => this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(arg);

    /// <summary>
    /// Converts this supplier to the delegate of type <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <returns>The delegate representing the wrapped method.</returns>
    public Func<T, TResult> ToDelegate() => DelegateHelpers.CreateDelegate<T, TResult>(ptr);

    /// <summary>
    /// Gets hexadecimal representation of this pointer.
    /// </summary>
    /// <returns>Hexadecimal representation of this pointer.</returns>
    public override string ToString() => new IntPtr(ptr).ToString("X");

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The pointer to the managed method.</param>
    /// <returns>The typed function pointer.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public static implicit operator Supplier<T, TResult>(delegate*<T, TResult> ptr) => new(ptr);

    /// <summary>
    /// Converts this supplier to the delegate of type <see cref="Func{T, TResult}"/>.
    /// </summary>
    /// <param name="supplier">The value representing the pointer to the method.</param>
    /// <returns>The delegate representing the wrapped method.</returns>
    public static explicit operator Func<T, TResult>(Supplier<T, TResult> supplier) => supplier.ToDelegate();
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T, TResult}"/> interface
/// with the support of closure that is not allocated on the heap.
/// </summary>
/// <typeparam name="TContext">The type describing closure.</typeparam>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
[CLSCompliant(false)]
public readonly unsafe struct SupplierClosure<TContext, T, TResult> : ISupplier<T, TResult>
{
    private readonly delegate*<in TContext, T, TResult> ptr;
    private readonly TContext context;

    /// <summary>
    /// Wraps the function pointer.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="context">The context to be passed to the function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public SupplierClosure(delegate*<in TContext, T, TResult> ptr, TContext context)
    {
        this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));
        this.context = context;
    }

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(in context, arg);
}

/// <summary>
/// Represents implementation of <see cref="ISupplier{T, TResult}"/> that delegates
/// invocation to the delegate of type <see cref="Func{T, TResult}"/>.
/// </summary>
/// <typeparam name="T">The type of the argument.</typeparam>
/// <typeparam name="TResult">The type of the result.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct DelegatingSupplier<T, TResult> : ISupplier<T, TResult>, IEquatable<DelegatingSupplier<T, TResult>>
{
    private readonly Func<T, TResult> func;

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public DelegatingSupplier(Func<T, TResult> func)
        => this.func = func ?? throw new ArgumentNullException(nameof(func));

    /// <summary>
    /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => func is null;

    /// <inheritdoc />
    TResult ISupplier<T, TResult>.Invoke(T arg) => func(arg);

    /// <inheritdoc />
    Func<T, TResult> IFunctional<Func<T, TResult>>.ToDelegate() => func;

    /// <inheritdoc />
    public override string? ToString() => func?.ToString();

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="func">The delegate instance.</param>
    /// <returns>The supplier represented by the delegate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="func"/> is <see langword="null"/>.</exception>
    public static implicit operator DelegatingSupplier<T, TResult>(Func<T, TResult> func)
        => new(func);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DelegatingPredicate<T> : ISupplier<T, bool>, IFunctional<Predicate<T>>
{
    private readonly Predicate<T> predicate;

    internal DelegatingPredicate(Predicate<T> predicate)
        => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

    /// <inheritdoc />
    bool ISupplier<T, bool>.Invoke(T arg) => predicate(arg);

    /// <inheritdoc />
    Func<T, bool> IFunctional<Func<T, bool>>.ToDelegate() => predicate.ChangeType<Func<T, bool>>();

    /// <inheritdoc />
    Predicate<T> IFunctional<Predicate<T>>.ToDelegate() => predicate;

    public static implicit operator DelegatingPredicate<T>(Predicate<T> predicate)
        => new(predicate);
}

[StructLayout(LayoutKind.Auto)]
internal readonly struct DelegatingConverter<TInput, TOutput> : ISupplier<TInput, TOutput>, IFunctional<Converter<TInput, TOutput>>
{
    private readonly Converter<TInput, TOutput> converter;

    internal DelegatingConverter(Converter<TInput, TOutput> converter)
        => this.converter = converter ?? throw new ArgumentNullException(nameof(converter));

    /// <inheritdoc />
    TOutput ISupplier<TInput, TOutput>.Invoke(TInput arg) => converter(arg);

    /// <inheritdoc />
    Func<TInput, TOutput> IFunctional<Func<TInput, TOutput>>.ToDelegate() => converter.ChangeType<Func<TInput, TOutput>>();

    /// <inheritdoc />
    Converter<TInput, TOutput> IFunctional<Converter<TInput, TOutput>>.ToDelegate() => converter;

    public static implicit operator DelegatingConverter<TInput, TOutput>(Converter<TInput, TOutput> converter)
        => new(converter);
}