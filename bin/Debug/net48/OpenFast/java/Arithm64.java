import java.lang.foreign.*;
import java.lang.invoke.MethodHandle;
import java.nio.file.Path;

public class Arithm64 {
    private final MethodHandle fastAddHandle;

    public Arithm64() {
        try {
            Path dllPath = Path.of("./fast_addx64.dll").toAbsolutePath();

            SymbolLookup library = SymbolLookup.libraryLookup(dllPath, Arena.global());

            MemorySegment functionAddress = library.find("fast_addx64")
                .orElseThrow(() -> new RuntimeException("function not found"));

            FunctionDescriptor blueprint = FunctionDescriptor.of(
                ValueLayout.JAVA_LONG,
                ValueLayout.JAVA_LONG,
                ValueLayout.JAVA_LONG
            );

            this.fastAddHandle = Linker.nativeLinker().downcallHandle(functionAddress, blueprint);

        } catch (Exception e) {
            throw new RuntimeException("initialization failed", e);
        }
    }
    public long fast_addx64(long a, long b) {
        try {
            return (long) this.fastAddHandle.invokeExact(a,b);
        } catch (Throwable t) {
            throw new RuntimeException("Error executing raw assembly calculation", t);
        }
    }
    static void main() {
        Arithm64 asmBindgen = new Arithm64();
        long asmValue = asmBindgen.fast_addx64(2,5);
        System.out.println(asmValue);
    }
}
