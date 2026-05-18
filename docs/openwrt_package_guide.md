# Building a Universal OpenWrt Agent Package

To support both **Firmware Integration** and **Standalone Installation**, you must build your agent as a standard OpenWrt Package. This allows you to either select it in `make menuconfig` (for built-in) or generate an `.ipk` file (for manual install).

---

## 1. Project Structure
The best way to organize your code is to follow the standard OpenWrt package pattern.

```text
cms-agent/
├── Makefile                # The OpenWrt build instructions
├── src/                    # Source code directory
│   ├── main.c              # Core logic (Registration/MQTT)
│   ├── utils.c             # Helper functions (JSON parsing, etc.)
│   └── Makefile            # Standard Makefile for the C compiler
└── files/                  # Configuration and Init scripts
    ├── cms_agent.config    # Default config (installed to /etc/config/cms_agent)
    └── cms_agent.init      # Init script (installed to /etc/init.d/cms_agent)
```

---

## 2. The OpenWrt Makefile (The "Glue")
This file lives at the root of your package directory. It tells OpenWrt how to handle your package.

```makefile
include $(TOPDIR)/rules.mk

PKG_NAME:=cms-agent
PKG_VERSION:=1.0.0
PKG_RELEASE:=1

PKG_MAINTAINER:=YourName <you@example.com>
PKG_LICENSE:=MIT

include $(INCLUDE_DIR)/package.mk

define Package/cms-agent
  SECTION:=utils
  CATEGORY:=Utilities
  TITLE:=CMS Management Agent
  DEPENDS:=+libmosquitto +libuci +libcurl +libjson-c
endef

define Package/cms-agent/description
  A lightweight management agent for remote device monitoring and configuration.
endef

define Build/Prepare
	mkdir -p $(PKG_BUILD_DIR)
	$(CP) ./src/* $(PKG_BUILD_DIR)/
endef

define Package/cms-agent/install
	$(INSTALL_DIR) $(1)/usr/bin
	$(INSTALL_BIN) $(PKG_BUILD_DIR)/cms_agent $(1)/usr/bin/

	$(INSTALL_DIR) $(1)/etc/config
	$(INSTALL_CONF) ./files/cms_agent.config $(1)/etc/config/cms_agent

	$(INSTALL_DIR) $(1)/etc/init.d
	$(INSTALL_BIN) ./files/cms_agent.init $(1)/etc/init.d/cms_agent
endef

$(eval $(call BuildPackage,cms-agent))
```

---

## 3. The Init Script (`cms_agent.init`)
Using OpenWrt's `procd` system ensures your agent starts on boot and restarts automatically if it crashes.

```bash
#!/bin/sh /etc/rc.common

START=99
USE_PROCD=1

start_service() {
    procd_open_instance
    procd_set_param command /usr/bin/cms_agent
    procd_set_param respawn  # Automatically restart on exit
    procd_set_param stdout 1 # Capture logs to logread
    procd_set_param stderr 1
    procd_close_instance
}
```

---

## 4. How to Compile

### Method A: Standalone .ipk (For Separate Install)
1.  Download the **OpenWrt SDK** for your router's architecture.
2.  Place your `cms-agent` folder into the `package/` directory of the SDK.
3.  Run:
    ```bash
    make package/cms-agent/compile
    ```
4.  Your `.ipk` will be in `bin/packages/[arch]/base/`. You can install it with `opkg install cms-agent_1.0.0_arch.ipk`.

### Method B: Built-in (For Firmware Image)
1.  In your full **OpenWrt Source** tree, place the `cms-agent` folder in `package/`.
2.  Run `make menuconfig`.
3.  Find **Utilities** -> **cms-agent** and select `<*>` (Built-in).
4.  Run `make` to build the whole firmware image. Your agent will be pre-installed in `/usr/bin/`.

---

## 5. Recommended Core Libraries (C)
To keep the binary small and efficient:
*   **libmosquitto:** For the MQTT connection.
*   **libcurl:** For the initial HTTP Registration/Bootstrap and OTA downloads.
*   **libuci:** For reading/writing router settings natively.
*   **libjson-c:** For parsing the JSON payloads from your .NET CMS.
