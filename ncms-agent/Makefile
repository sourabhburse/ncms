include $(TOPDIR)/rules.mk

PKG_NAME:=NCMS
PKG_VERSION:=1.0
PKG_RELEASE:=14

PKG_MAINTAINER:=Aashish Kulkarni <aashish@niseva.com>
PKG_LICENSE:=CC0-1.0

include $(INCLUDE_DIR)/package.mk
include $(INCLUDE_DIR)/cmake.mk

define Package/$(PKG_NAME)
	SECTION:=niseva
	CATEGORY:=xNet
	SUBMENU:=NCMS
	TITLE:=xNet NCMS
	DEPENDS:=+libuci +libjson-c  +libcurl +libopenssl +libmosquitto
	URL:=https://www.niseva.com
endef

define Package/$(PKG_NAME)/description
	NCMS application.
endef

define Build/Prepare
	mkdir -p $(PKG_BUILD_DIR)
	$(CP) ./src/* $(PKG_BUILD_DIR)/
endef

define Package/$(PKG_NAME)/install
	$(CP) ./files/* $(1)/

	$(INSTALL_DIR) $(1)/usr/bin
	$(INSTALL_BIN) $(PKG_BUILD_DIR)/NCMS $(1)/usr/bin/
endef

$(eval $(call BuildPackage,$(PKG_NAME)))

