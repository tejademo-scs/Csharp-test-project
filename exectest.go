//vulnerable function, no default case in switch statement
func vulnFunc(op string) {
    switch str {
    case "1":
        fmt.Println("one")
    case "2":
        fmt.Println("two")
    // No default case
    }
}
 
//vulnerable function, command injection
func vulnFunc2(cmd string) {
    exec.Command("bash", "-c", cmd).Run()
}
 
//vulnerable function, copy string
func CopyString(s string) string {
    b := []byte(s)
    return string(b)
}
 
//vulnerable function, code injection
func executeCommand(command string) {
                out, err := exec.Command("sh", "-c", command).Output()
                if err != nil {
                                fmt.Printf("Error: %s\n", err)
                                return
                }
                fmt.Printf("Output: %s\n", out)
}
