// PanBridge's rclone entry point. Keep the upstream VFS/cache and Windows
// cmount implementation, importing only the backends and commands we use.
package main

import (
	_ "github.com/rclone/rclone/backend/local"
	_ "github.com/rclone/rclone/backend/webdav"
	"github.com/rclone/rclone/cmd"
	_ "github.com/rclone/rclone/cmd/cmount"
	_ "github.com/rclone/rclone/cmd/obscure"
	_ "github.com/rclone/rclone/cmd/version"
)

func main() { cmd.Main() }
