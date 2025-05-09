package example.authz

# Allow access if the user has the "admin" role
allow {
    input.method = "GET"
    input.path = ["admin"]
    input.user.role == "admin"
}

# Allow access if the user has the "user" role
allow {
    input.method = "GET"
    input.path = ["user"]
    input.user.role == "user"
}

# Deny access to "admin" paths for non-admin users
deny {
    input.method = "GET"
    input.path = ["admin"]
    input.user.role != "admin"
}

# Allow access to everyone for "public" path
allow {
    input.method = "GET"
    input.path = ["public"]
}

# Default deny rule
default allow = false
