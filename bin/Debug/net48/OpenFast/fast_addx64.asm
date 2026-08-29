section .text
global fastadd

fast_addx64:
    
    mov rax,rcx
    add rax,rdx
    
    ret